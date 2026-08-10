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
                (await _db.Services
                    .Where(s => s.AffiliateId == affiliateId)
                    .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
                    .ToListAsync())
                    .Select(CatalogItemMapper.FromService).ToList(),

            BusinessType.Retail =>
                (await _db.InventoryItems
                    .Where(i => i.AffiliateId == affiliateId)
                    .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
                    .ToListAsync())
                    .Select(CatalogItemMapper.FromInventoryItem).ToList(),

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
                MapNullable(await _db.Services
                    .FirstOrDefaultAsync(s => s.AffiliateId == affiliateId && s.Id == itemId),
                    CatalogItemMapper.FromService),

            BusinessType.Retail =>
                MapNullable(await _db.InventoryItems
                    .FirstOrDefaultAsync(i => i.AffiliateId == affiliateId && i.Id == itemId),
                    CatalogItemMapper.FromInventoryItem),

            _ => null
        };
    }

    private static CatalogItemDto? MapNullable<TEntity>(TEntity? entity, Func<TEntity, CatalogItemDto> map)
        where TEntity : class
        => entity is null ? null : map(entity);

    // Galería — resuelve (ImageUrl, ImagesJson) para creación: si viene una galería, Images[0]
    // gana sobre ImageUrl; si no, se usa ImageUrl tal cual (compatibilidad hacia atrás).
    private static (string? ImageUrl, string? ImagesJson) ResolveImagesForCreate(
        IReadOnlyList<string>? images, string? fallbackImageUrl)
    {
        if (images is null) return (fallbackImageUrl, null);
        if (images.Count == 0) return (null, null);
        return (images[0], JsonArrayField.Serialize(images));
    }

    // Galería — tri-estado para updates: null = no tocar la galería; [] = vaciarla
    // (ImageUrl también queda null); lista = reemplazarla entera (ImageUrl = Images[0]).
    // Si Images es null pero viene ImageUrl suelto, se respeta el comportamiento previo.
    private static void ApplyImagesPatch(
        IReadOnlyList<string>? images, string? imageUrl,
        Action<string?> setImageUrl, Action<string?> setImages)
    {
        if (images is not null)
        {
            setImages(images.Count == 0 ? null : JsonArrayField.Serialize(images));
            setImageUrl(images.Count > 0 ? images[0] : null);
        }
        else if (imageUrl is not null)
        {
            setImageUrl(imageUrl);
        }
    }

    public async Task<CatalogItemDto> CreateItemAsync(Guid affiliateId, CreateCatalogItemRequest request)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException($"Affiliate {affiliateId} not found.");

        if (_planLimit.IsTrialExpired(affiliate))
            throw new InvalidOperationException(PlanLimitService.TrialExpiredMessage);

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

        if (_planLimit.IsTrialExpired(affiliate))
            throw new InvalidOperationException(PlanLimitService.TrialExpiredMessage);

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
        ApplyImagesPatch(request.Images, request.ImageUrl, v => product.ImageUrl = v, v => product.Images = v);
        if (request.SortOrder.HasValue) product.SortOrder = request.SortOrder.Value;
        if (request.IsPubliclyVisible.HasValue) product.IsPubliclyVisible = request.IsPubliclyVisible.Value;
        if (request.Status is not null) product.Status = request.Status;
        if (request.DescriptionEn is not null) product.DescriptionEn = request.DescriptionEn;
        if (request.Periods is not null) product.Periods = TokenList.Join(request.Periods);
        if (request.WeekDays is not null) product.WeekDays = TokenList.Join(request.WeekDays);
        if (request.Flags is not null) product.Flags = TokenList.Join(request.Flags);
        if (request.Featured.HasValue) product.Featured = request.Featured.Value;
        if (request.Popular.HasValue) product.Popular = request.Popular.Value;
        if (request.VideoUrl is not null) product.VideoUrl = request.VideoUrl;
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
        ApplyImagesPatch(request.Images, request.ImageUrl, v => service.ImageUrl = v, v => service.Images = v);
        if (request.SortOrder.HasValue) service.SortOrder = request.SortOrder.Value;
        if (request.IsPubliclyVisible.HasValue) service.IsPubliclyVisible = request.IsPubliclyVisible.Value;
        if (request.DurationMinutes.HasValue) service.DurationMinutes = request.DurationMinutes.Value;
        if (request.Status is not null) service.Status = request.Status;
        if (request.DescriptionEn is not null) service.DescriptionEn = request.DescriptionEn;
        if (wasDemo) service.IsDemo = false;

        await _db.SaveChangesAsync();
        return (CatalogItemMapper.FromService(service), wasDemo);
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
        ApplyImagesPatch(request.Images, request.ImageUrl, v => item.ImageUrl = v, v => item.Images = v);
        if (request.SortOrder.HasValue) item.SortOrder = request.SortOrder.Value;
        if (request.IsPubliclyVisible.HasValue) item.IsPubliclyVisible = request.IsPubliclyVisible.Value;
        if (request.Stock.HasValue) item.Quantity = request.Stock.Value;
        if (request.Status is not null) item.Status = request.Status;
        if (request.DescriptionEn is not null) item.DescriptionEn = request.DescriptionEn;
        if (wasDemo) item.IsDemo = false;

        await _db.SaveChangesAsync();
        return (CatalogItemMapper.FromInventoryItem(item), wasDemo);
    }

    // ── Create helpers ────────────────────────────────────────────

    private async Task<CatalogItemDto> CreateProductAsync(Guid affiliateId, CreateCatalogItemRequest request)
    {
        ValidateProductTokens(request.Periods, request.WeekDays);
        var (productImageUrl, productImagesJson) = ResolveImagesForCreate(request.Images, request.ImageUrl);

        var product = new Product
        {
            AffiliateId = affiliateId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            ImageUrl = productImageUrl,
            Images = productImagesJson,
            SortOrder = request.SortOrder,
            IsPubliclyVisible = true,
            IsDemo = false,
            Status = "Active",
            DescriptionEn = request.DescriptionEn,
            Periods = TokenList.Join(request.Periods),
            WeekDays = TokenList.Join(request.WeekDays),
            Flags = TokenList.Join(request.Flags),
            Featured = request.Featured ?? false,
            Popular = request.Popular ?? false,
            VideoUrl = request.VideoUrl
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return CatalogItemMapper.FromProduct(product);
    }

    private async Task<CatalogItemDto> CreateServiceAsync(Guid affiliateId, CreateCatalogItemRequest request)
    {
        var (serviceImageUrl, serviceImagesJson) = ResolveImagesForCreate(request.Images, request.ImageUrl);

        var service = new Service
        {
            AffiliateId = affiliateId,
            Name = request.Name,
            Description = request.Description,
            DescriptionEn = request.DescriptionEn,
            Price = request.Price,
            Category = request.Category,
            ImageUrl = serviceImageUrl,
            Images = serviceImagesJson,
            SortOrder = request.SortOrder,
            DurationMinutes = request.DurationMinutes ?? 30,
            IsPubliclyVisible = true,
            IsDemo = false,
            Status = "Active"
        };
        _db.Services.Add(service);
        await _db.SaveChangesAsync();
        return CatalogItemMapper.FromService(service);
    }

    private async Task<CatalogItemDto> CreateInventoryItemAsync(Guid affiliateId, CreateCatalogItemRequest request)
    {
        var (itemImageUrl, itemImagesJson) = ResolveImagesForCreate(request.Images, request.ImageUrl);

        var item = new InventoryItem
        {
            AffiliateId = affiliateId,
            Name = request.Name,
            Description = request.Description,
            DescriptionEn = request.DescriptionEn,
            UnitPrice = request.Price,
            Category = request.Category,
            ImageUrl = itemImageUrl,
            Images = itemImagesJson,
            SortOrder = request.SortOrder,
            Quantity = request.Stock ?? 0,
            IsPubliclyVisible = true,
            IsDemo = false,
            Status = "Active"
        };
        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();
        return CatalogItemMapper.FromInventoryItem(item);
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
        ApplyImagesPatch(request.Images, request.ImageUrl, v => product.ImageUrl = v, v => product.Images = v);
        if (request.SortOrder.HasValue) product.SortOrder = request.SortOrder.Value;
        if (request.IsPubliclyVisible.HasValue) product.IsPubliclyVisible = request.IsPubliclyVisible.Value;

        await _db.SaveChangesAsync();
        return CatalogItemMapper.FromProduct(product);
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
        ApplyImagesPatch(request.Images, request.ImageUrl, v => service.ImageUrl = v, v => service.Images = v);
        if (request.SortOrder.HasValue) service.SortOrder = request.SortOrder.Value;
        if (request.IsPubliclyVisible.HasValue) service.IsPubliclyVisible = request.IsPubliclyVisible.Value;
        if (request.DurationMinutes.HasValue) service.DurationMinutes = request.DurationMinutes.Value;

        await _db.SaveChangesAsync();
        return CatalogItemMapper.FromService(service);
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
        ApplyImagesPatch(request.Images, request.ImageUrl, v => item.ImageUrl = v, v => item.Images = v);
        if (request.SortOrder.HasValue) item.SortOrder = request.SortOrder.Value;
        if (request.IsPubliclyVisible.HasValue) item.IsPubliclyVisible = request.IsPubliclyVisible.Value;
        if (request.Stock.HasValue) item.Quantity = request.Stock.Value;

        await _db.SaveChangesAsync();
        return CatalogItemMapper.FromInventoryItem(item);
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
