using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class CatalogCrudService : ICatalogCrudService
{
    private readonly AppDbContext _db;
    private readonly IPlanLimitService _planLimit;

    public CatalogCrudService(AppDbContext db, IPlanLimitService planLimit)
    {
        _db = db;
        _planLimit = planLimit;
    }

    public async Task<List<CatalogItemDto>> GetItemsAsync(Guid affiliateId)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId);
        if (affiliate == null) return new List<CatalogItemDto>();

        if (affiliate.BusinessType is BusinessType.Restaurant or BusinessType.Creator or BusinessType.Publisher)
        {
            var products = await _db.Products
                .Where(p => p.AffiliateId == affiliateId)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
                .ToListAsync();
            return products.Select(CatalogItemMapper.FromProduct).ToList();
        }

        return affiliate.BusinessType switch
        {
            BusinessType.Barber or BusinessType.Service or BusinessType.Professional =>
                await _db.Services
                    .Where(s => s.AffiliateId == affiliateId)
                    .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
                    .Select(s => new CatalogItemDto(
                        s.Id, s.Name, s.Description, s.Price,
                        s.Category, s.ImageUrl, s.SortOrder, s.IsDemo,
                        s.DurationMinutes, null, s.Status,
                        s.DescriptionEn, null, null, null, null, null))
                    .ToListAsync(),

            BusinessType.Retail =>
                await _db.InventoryItems
                    .Where(i => i.AffiliateId == affiliateId)
                    .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
                    .Select(i => new CatalogItemDto(
                        i.Id, i.Name, i.Description, i.UnitPrice,
                        i.Category, i.ImageUrl, i.SortOrder, i.IsDemo,
                        null, i.Quantity, i.Status,
                        i.DescriptionEn, null, null, null, null, null))
                    .ToListAsync(),

            _ => new List<CatalogItemDto>()
        };
    }

    public async Task<CatalogItemDto?> GetItemAsync(Guid affiliateId, Guid itemId)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId);
        if (affiliate == null) return null;

        if (affiliate.BusinessType is BusinessType.Restaurant or BusinessType.Creator or BusinessType.Publisher)
        {
            var product = await _db.Products
                .FirstOrDefaultAsync(p => p.AffiliateId == affiliateId && p.Id == itemId);
            return product == null ? null : CatalogItemMapper.FromProduct(product);
        }

        return affiliate.BusinessType switch
        {
            BusinessType.Barber or BusinessType.Service or BusinessType.Professional =>
                await _db.Services
                    .Where(s => s.AffiliateId == affiliateId && s.Id == itemId)
                    .Select(s => new CatalogItemDto(
                        s.Id, s.Name, s.Description, s.Price,
                        s.Category, s.ImageUrl, s.SortOrder, s.IsDemo,
                        s.DurationMinutes, null, s.Status,
                        s.DescriptionEn, null, null, null, null, null))
                    .FirstOrDefaultAsync(),

            BusinessType.Retail =>
                await _db.InventoryItems
                    .Where(i => i.AffiliateId == affiliateId && i.Id == itemId)
                    .Select(i => new CatalogItemDto(
                        i.Id, i.Name, i.Description, i.UnitPrice,
                        i.Category, i.ImageUrl, i.SortOrder, i.IsDemo,
                        null, i.Quantity, i.Status,
                        i.DescriptionEn, null, null, null, null, null))
                    .FirstOrDefaultAsync(),

            _ => null
        };
    }

    public async Task<CatalogItemDto> CreateItemAsync(Guid affiliateId, CreateCatalogItemRequest request)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException($"Affiliate {affiliateId} not found.");

        if (!await _planLimit.CanAddItemAsync(affiliateId))
            throw new InvalidOperationException("Plan limit reached. Max 10 items on Free plan.");

        return affiliate.BusinessType switch
        {
            BusinessType.Restaurant or BusinessType.Creator or BusinessType.Publisher =>
                await CreateProductAsync(affiliateId, request),

            BusinessType.Barber or BusinessType.Service or BusinessType.Professional =>
                await CreateServiceAsync(affiliateId, request),

            BusinessType.Retail =>
                await CreateInventoryItemAsync(affiliateId, request),

            _ => throw new InvalidOperationException($"Unsupported BusinessType: {affiliate.BusinessType}")
        };
    }

    public async Task<CatalogItemDto?> UpdateItemAsync(Guid affiliateId, Guid itemId, UpdateCatalogItemRequest request)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId);
        if (affiliate == null) return null;

        return affiliate.BusinessType switch
        {
            BusinessType.Restaurant or BusinessType.Creator or BusinessType.Publisher =>
                await UpdateProductAsync(affiliateId, itemId, request),

            BusinessType.Barber or BusinessType.Service or BusinessType.Professional =>
                await UpdateServiceAsync(affiliateId, itemId, request),

            BusinessType.Retail =>
                await UpdateInventoryItemAsync(affiliateId, itemId, request),

            _ => null
        };
    }

    public async Task<bool> DeleteItemAsync(Guid affiliateId, Guid itemId)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId);
        if (affiliate == null) return false;

        return affiliate.BusinessType switch
        {
            BusinessType.Restaurant or BusinessType.Creator or BusinessType.Publisher =>
                await DeleteProductAsync(affiliateId, itemId),

            BusinessType.Barber or BusinessType.Service or BusinessType.Professional =>
                await DeleteServiceAsync(affiliateId, itemId),

            BusinessType.Retail =>
                await DeleteInventoryItemAsync(affiliateId, itemId),

            _ => false
        };
    }

    public async Task<(CatalogItemDto Item, bool WasDemo)> UpdateAsync(
        string supabaseUserId, Guid affiliateId, Guid itemId, UpdateCatalogItemRequest request)
    {
        var hasAccess = await _db.UserAffiliateMaps
            .AnyAsync(m => m.SupabaseUserId == supabaseUserId && m.AffiliateId == affiliateId);
        if (!hasAccess) throw new UnauthorizedAccessException();

        if (request.Price.HasValue && request.Price.Value < 0)
            throw new ArgumentException("Price cannot be negative.");

        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException($"Affiliate {affiliateId} not found.");

        return affiliate.BusinessType switch
        {
            BusinessType.Restaurant or BusinessType.Creator or BusinessType.Publisher =>
                await PatchProductAsync(affiliateId, itemId, request),

            BusinessType.Barber or BusinessType.Service or BusinessType.Professional =>
                await PatchServiceAsync(affiliateId, itemId, request),

            BusinessType.Retail =>
                await PatchInventoryItemAsync(affiliateId, itemId, request),

            _ => throw new KeyNotFoundException($"Item {itemId} not found.")
        };
    }

    private async Task<(CatalogItemDto, bool)> PatchProductAsync(Guid affiliateId, Guid itemId, UpdateCatalogItemRequest request)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == itemId && p.AffiliateId == affiliateId)
            ?? throw new KeyNotFoundException();

        ValidateProductTokens(request.Periods, request.WeekDays);

        var wasDemo = product.IsDemo;
        if (request.Name is not null) product.Name = request.Name;
        if (request.Description is not null) product.Description = request.Description;
        if (request.Category is not null) product.Category = request.Category;
        if (request.Price.HasValue) product.Price = request.Price.Value;
        if (request.ImageUrl is not null) product.ImageUrl = request.ImageUrl;
        if (request.SortOrder.HasValue) product.SortOrder = request.SortOrder.Value;
        if (request.IsPubliclyVisible.HasValue) product.IsPubliclyVisible = request.IsPubliclyVisible.Value;
        if (request.Status is not null) product.Status = request.Status;
        if (request.DescriptionEn is not null) product.DescriptionEn = request.DescriptionEn;
        if (request.Periods is not null) product.Periods = TokenList.Join(request.Periods);
        if (request.WeekDays is not null) product.WeekDays = TokenList.Join(request.WeekDays);
        if (request.Flags is not null) product.Flags = TokenList.Join(request.Flags);
        if (request.Featured.HasValue) product.Featured = request.Featured.Value;
        if (request.Popular.HasValue) product.Popular = request.Popular.Value;
        if (wasDemo) product.IsDemo = false;

        await _db.SaveChangesAsync();
        return (CatalogItemMapper.FromProduct(product), wasDemo);
    }

    private async Task<(CatalogItemDto, bool)> PatchServiceAsync(Guid affiliateId, Guid itemId, UpdateCatalogItemRequest request)
    {
        var service = await _db.Services
            .FirstOrDefaultAsync(s => s.Id == itemId && s.AffiliateId == affiliateId)
            ?? throw new KeyNotFoundException();

        var wasDemo = service.IsDemo;
        if (request.Name is not null) service.Name = request.Name;
        if (request.Description is not null) service.Description = request.Description;
        if (request.Category is not null) service.Category = request.Category;
        if (request.Price.HasValue) service.Price = request.Price.Value;
        if (request.ImageUrl is not null) service.ImageUrl = request.ImageUrl;
        if (request.SortOrder.HasValue) service.SortOrder = request.SortOrder.Value;
        if (request.IsPubliclyVisible.HasValue) service.IsPubliclyVisible = request.IsPubliclyVisible.Value;
        if (request.DurationMinutes.HasValue) service.DurationMinutes = request.DurationMinutes.Value;
        if (request.Status is not null) service.Status = request.Status;
        if (request.DescriptionEn is not null) service.DescriptionEn = request.DescriptionEn;
        if (wasDemo) service.IsDemo = false;

        await _db.SaveChangesAsync();
        return (new CatalogItemDto(service.Id, service.Name, service.Description, service.Price,
            service.Category, service.ImageUrl, service.SortOrder, service.IsDemo,
            service.DurationMinutes, null, service.Status,
            service.DescriptionEn), wasDemo);
    }

    private async Task<(CatalogItemDto, bool)> PatchInventoryItemAsync(Guid affiliateId, Guid itemId, UpdateCatalogItemRequest request)
    {
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.AffiliateId == affiliateId)
            ?? throw new KeyNotFoundException();

        var wasDemo = item.IsDemo;
        if (request.Name is not null) item.Name = request.Name;
        if (request.Description is not null) item.Description = request.Description;
        if (request.Category is not null) item.Category = request.Category;
        if (request.Price.HasValue) item.UnitPrice = request.Price.Value;
        if (request.ImageUrl is not null) item.ImageUrl = request.ImageUrl;
        if (request.SortOrder.HasValue) item.SortOrder = request.SortOrder.Value;
        if (request.IsPubliclyVisible.HasValue) item.IsPubliclyVisible = request.IsPubliclyVisible.Value;
        if (request.Stock.HasValue) item.Quantity = request.Stock.Value;
        if (request.Status is not null) item.Status = request.Status;
        if (request.DescriptionEn is not null) item.DescriptionEn = request.DescriptionEn;
        if (wasDemo) item.IsDemo = false;

        await _db.SaveChangesAsync();
        return (new CatalogItemDto(item.Id, item.Name, item.Description, item.UnitPrice,
            item.Category, item.ImageUrl, item.SortOrder, item.IsDemo,
            null, item.Quantity, item.Status,
            item.DescriptionEn), wasDemo);
    }

    // ── Create helpers ────────────────────────────────────────────

    private async Task<CatalogItemDto> CreateProductAsync(Guid affiliateId, CreateCatalogItemRequest request)
    {
        ValidateProductTokens(request.Periods, request.WeekDays);

        var product = new Product
        {
            AffiliateId = affiliateId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            ImageUrl = request.ImageUrl,
            SortOrder = request.SortOrder,
            IsPubliclyVisible = true,
            IsDemo = false,
            Status = "Active",
            DescriptionEn = request.DescriptionEn,
            Periods = TokenList.Join(request.Periods),
            WeekDays = TokenList.Join(request.WeekDays),
            Flags = TokenList.Join(request.Flags),
            Featured = request.Featured ?? false,
            Popular = request.Popular ?? false
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return CatalogItemMapper.FromProduct(product);
    }

    private async Task<CatalogItemDto> CreateServiceAsync(Guid affiliateId, CreateCatalogItemRequest request)
    {
        var service = new Service
        {
            AffiliateId = affiliateId,
            Name = request.Name,
            Description = request.Description,
            DescriptionEn = request.DescriptionEn,
            Price = request.Price,
            Category = request.Category,
            ImageUrl = request.ImageUrl,
            SortOrder = request.SortOrder,
            DurationMinutes = request.DurationMinutes ?? 30,
            IsPubliclyVisible = true,
            IsDemo = false,
            Status = "Active"
        };
        _db.Services.Add(service);
        await _db.SaveChangesAsync();
        return new CatalogItemDto(service.Id, service.Name, service.Description, service.Price,
            service.Category, service.ImageUrl, service.SortOrder, service.IsDemo,
            service.DurationMinutes, null, service.Status,
            service.DescriptionEn);
    }

    private async Task<CatalogItemDto> CreateInventoryItemAsync(Guid affiliateId, CreateCatalogItemRequest request)
    {
        var item = new InventoryItem
        {
            AffiliateId = affiliateId,
            Name = request.Name,
            Description = request.Description,
            DescriptionEn = request.DescriptionEn,
            UnitPrice = request.Price,
            Category = request.Category,
            ImageUrl = request.ImageUrl,
            SortOrder = request.SortOrder,
            Quantity = request.Stock ?? 0,
            IsPubliclyVisible = true,
            IsDemo = false,
            Status = "Active"
        };
        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();
        return new CatalogItemDto(item.Id, item.Name, item.Description, item.UnitPrice,
            item.Category, item.ImageUrl, item.SortOrder, item.IsDemo,
            null, item.Quantity, item.Status,
            item.DescriptionEn);
    }

    // ── Update helpers ────────────────────────────────────────────

    private async Task<CatalogItemDto?> UpdateProductAsync(Guid affiliateId, Guid itemId, UpdateCatalogItemRequest request)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.AffiliateId == affiliateId && p.Id == itemId);
        if (product == null) return null;

        if (product.IsDemo)
        {
            if (!await _planLimit.CanAddItemAsync(affiliateId))
                throw new InvalidOperationException("Plan limit reached. Max 10 items on Free plan.");
            product.IsDemo = false;
        }

        if (request.Name != null) product.Name = request.Name;
        if (request.Description != null) product.Description = request.Description;
        if (request.Price.HasValue) product.Price = request.Price.Value;
        if (request.Category != null) product.Category = request.Category;
        if (request.ImageUrl != null) product.ImageUrl = request.ImageUrl;
        if (request.SortOrder.HasValue) product.SortOrder = request.SortOrder.Value;
        if (request.IsPubliclyVisible.HasValue) product.IsPubliclyVisible = request.IsPubliclyVisible.Value;

        await _db.SaveChangesAsync();
        return new CatalogItemDto(product.Id, product.Name, product.Description, product.Price,
            product.Category, product.ImageUrl, product.SortOrder, product.IsDemo,
            null, null, product.Status);
    }

    private async Task<CatalogItemDto?> UpdateServiceAsync(Guid affiliateId, Guid itemId, UpdateCatalogItemRequest request)
    {
        var service = await _db.Services
            .FirstOrDefaultAsync(s => s.AffiliateId == affiliateId && s.Id == itemId);
        if (service == null) return null;

        if (service.IsDemo)
        {
            if (!await _planLimit.CanAddItemAsync(affiliateId))
                throw new InvalidOperationException("Plan limit reached. Max 10 items on Free plan.");
            service.IsDemo = false;
        }

        if (request.Name != null) service.Name = request.Name;
        if (request.Description != null) service.Description = request.Description;
        if (request.Price.HasValue) service.Price = request.Price.Value;
        if (request.Category != null) service.Category = request.Category;
        if (request.ImageUrl != null) service.ImageUrl = request.ImageUrl;
        if (request.SortOrder.HasValue) service.SortOrder = request.SortOrder.Value;
        if (request.IsPubliclyVisible.HasValue) service.IsPubliclyVisible = request.IsPubliclyVisible.Value;
        if (request.DurationMinutes.HasValue) service.DurationMinutes = request.DurationMinutes.Value;

        await _db.SaveChangesAsync();
        return new CatalogItemDto(service.Id, service.Name, service.Description, service.Price,
            service.Category, service.ImageUrl, service.SortOrder, service.IsDemo,
            service.DurationMinutes, null, service.Status);
    }

    private async Task<CatalogItemDto?> UpdateInventoryItemAsync(Guid affiliateId, Guid itemId, UpdateCatalogItemRequest request)
    {
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.AffiliateId == affiliateId && i.Id == itemId);
        if (item == null) return null;

        if (item.IsDemo)
        {
            if (!await _planLimit.CanAddItemAsync(affiliateId))
                throw new InvalidOperationException("Plan limit reached. Max 10 items on Free plan.");
            item.IsDemo = false;
        }

        if (request.Name != null) item.Name = request.Name;
        if (request.Description != null) item.Description = request.Description;
        if (request.Price.HasValue) item.UnitPrice = request.Price.Value;
        if (request.Category != null) item.Category = request.Category;
        if (request.ImageUrl != null) item.ImageUrl = request.ImageUrl;
        if (request.SortOrder.HasValue) item.SortOrder = request.SortOrder.Value;
        if (request.IsPubliclyVisible.HasValue) item.IsPubliclyVisible = request.IsPubliclyVisible.Value;
        if (request.Stock.HasValue) item.Quantity = request.Stock.Value;

        await _db.SaveChangesAsync();
        return new CatalogItemDto(item.Id, item.Name, item.Description, item.UnitPrice,
            item.Category, item.ImageUrl, item.SortOrder, item.IsDemo,
            null, item.Quantity, item.Status);
    }

    // ── Validation ──────────────────────────────────────────────────

    private static void ValidateProductTokens(IReadOnlyList<string>? periods, IReadOnlyList<string>? weekDays)
    {
        if (periods != null)
            foreach (var p in periods)
                if (!MealPeriodTokens.Whitelist.Contains(p))
                    throw new ArgumentException($"Unsupported period: {p}");

        if (weekDays != null)
            foreach (var d in weekDays)
                if (!WeekDayTokens.Whitelist.Contains(d))
                    throw new ArgumentException($"Unsupported week day: {d}");
    }

    // ── Delete helpers ────────────────────────────────────────────

    private async Task<bool> DeleteProductAsync(Guid affiliateId, Guid itemId)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.AffiliateId == affiliateId && p.Id == itemId);
        if (product == null) return false;
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<bool> DeleteServiceAsync(Guid affiliateId, Guid itemId)
    {
        var service = await _db.Services
            .FirstOrDefaultAsync(s => s.AffiliateId == affiliateId && s.Id == itemId);
        if (service == null) return false;
        _db.Services.Remove(service);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<bool> DeleteInventoryItemAsync(Guid affiliateId, Guid itemId)
    {
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.AffiliateId == affiliateId && i.Id == itemId);
        if (item == null) return false;
        _db.InventoryItems.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }
}
