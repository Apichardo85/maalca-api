using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>
/// Grupos de modificadores reutilizables (Restaurante) — ej. un solo grupo "Guarnición" enlazado
/// a los 8 items de Fritura en vez de duplicar cada plato en "sin guarnición"/"con guarnición".
/// Mismo patrón que InventoryService/Receta: reemplazo total por PUT al asignar grupos a un
/// producto, no incremental.
/// </summary>
public class ModifierService : IModifierService
{
    private readonly AppDbContext _context;

    public ModifierService(AppDbContext context) => _context = context;

    public async Task<List<ModifierGroupDto>> GetGroupsAsync(Guid affiliateId)
    {
        var groups = await _context.ModifierGroups
            .Where(g => g.AffiliateId == affiliateId)
            .Include(g => g.Options)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
            .ToListAsync();
        return groups.Select(ToDto).ToList();
    }

    public async Task<ModifierGroupDto?> GetGroupAsync(Guid affiliateId, Guid id)
    {
        var group = await _context.ModifierGroups
            .Where(g => g.Id == id && g.AffiliateId == affiliateId)
            .Include(g => g.Options)
            .FirstOrDefaultAsync();
        return group == null ? null : ToDto(group);
    }

    public async Task<ModifierGroupDto> CreateGroupAsync(Guid affiliateId, CreateModifierGroupRequest request)
    {
        var now = DateTime.UtcNow;
        var group = new ModifierGroup
        {
            Id = Guid.NewGuid(),
            AffiliateId = affiliateId,
            Name = request.Name,
            NameEn = request.NameEn,
            MinSelect = request.MinSelect,
            MaxSelect = request.MaxSelect,
            Required = request.Required,
            SortOrder = request.SortOrder,
            CreatedAt = now,
        };
        foreach (var opt in request.Options)
        {
            group.Options.Add(new ModifierOption
            {
                Id = Guid.NewGuid(),
                ModifierGroupId = group.Id,
                Name = opt.Name,
                NameEn = opt.NameEn,
                PriceDelta = opt.PriceDelta,
                IsDefault = opt.IsDefault,
                SortOrder = opt.SortOrder,
                CreatedAt = now,
            });
        }
        _context.ModifierGroups.Add(group);
        await _context.SaveChangesAsync();
        return ToDto(group);
    }

    public async Task<ModifierGroupDto?> UpdateGroupAsync(Guid affiliateId, Guid id, UpdateModifierGroupRequest request)
    {
        var group = await _context.ModifierGroups
            .Where(g => g.Id == id && g.AffiliateId == affiliateId)
            .Include(g => g.Options)
            .FirstOrDefaultAsync();
        if (group == null) return null;

        if (request.Name != null) group.Name = request.Name;
        if (request.NameEn != null) group.NameEn = request.NameEn;
        if (request.MinSelect.HasValue) group.MinSelect = request.MinSelect.Value;
        if (request.MaxSelect.HasValue) group.MaxSelect = request.MaxSelect.Value;
        if (request.Required.HasValue) group.Required = request.Required.Value;
        if (request.SortOrder.HasValue) group.SortOrder = request.SortOrder.Value;

        // Options == null -> no tocar; lista -> reemplazo total (mismo patrón que SetRecipeAsync).
        if (request.Options != null)
        {
            _context.ModifierOptions.RemoveRange(group.Options);
            var now = DateTime.UtcNow;
            foreach (var opt in request.Options)
            {
                _context.ModifierOptions.Add(new ModifierOption
                {
                    Id = Guid.NewGuid(),
                    ModifierGroupId = group.Id,
                    Name = opt.Name,
                    NameEn = opt.NameEn,
                    PriceDelta = opt.PriceDelta,
                    IsDefault = opt.IsDefault,
                    SortOrder = opt.SortOrder,
                    CreatedAt = now,
                });
            }
        }

        group.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return await GetGroupAsync(affiliateId, id);
    }

    /// <summary>Borrar un grupo enlazado a uno o más productos se bloquea con un mensaje claro
    /// (qué productos lo usan) — mismo patrón que InventoryService.DeleteInventoryItemAsync con
    /// ProductIngredient, para no dejar un Product con un vínculo roto en silencio.</summary>
    public async Task<bool> DeleteGroupAsync(Guid affiliateId, Guid id)
    {
        var group = await _context.ModifierGroups.FirstOrDefaultAsync(g => g.Id == id && g.AffiliateId == affiliateId);
        if (group == null) return false;

        var usedByProducts = await _context.ProductModifierGroups
            .Where(pmg => pmg.ModifierGroupId == id)
            .Include(pmg => pmg.Product)
            .Where(pmg => pmg.Product != null)
            .Select(pmg => pmg.Product!.Name)
            .Distinct()
            .ToListAsync();
        if (usedByProducts.Count > 0)
            throw new InvalidOperationException(
                $"No se puede eliminar \"{group.Name}\": está enlazado a {string.Join(", ", usedByProducts)}. Quítalo de esos productos primero.");

        _context.ModifierGroups.Remove(group);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<ModifierGroupDto>> GetProductModifierGroupsAsync(Guid affiliateId, Guid productId)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && p.AffiliateId == affiliateId);
        if (product == null) return new List<ModifierGroupDto>();

        var groups = await _context.ProductModifierGroups
            .Where(pmg => pmg.ProductId == productId)
            .Include(pmg => pmg.ModifierGroup!).ThenInclude(g => g.Options)
            .Where(pmg => pmg.ModifierGroup != null)
            .OrderBy(pmg => pmg.SortOrder)
            .Select(pmg => pmg.ModifierGroup!)
            .ToListAsync();
        return groups.Select(ToDto).ToList();
    }

    public async Task<List<ModifierGroupDto>> SetProductModifierGroupsAsync(Guid affiliateId, Guid productId, List<Guid> modifierGroupIds)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && p.AffiliateId == affiliateId);
        if (product == null)
            throw new InvalidOperationException("Product not found");

        // Validar que todos los ModifierGroup pertenezcan al mismo afiliado — evita enlazar, sea
        // por error o manipulando el request, un grupo de otro negocio.
        var distinctIds = modifierGroupIds.Distinct().ToList();
        var validIds = await _context.ModifierGroups
            .Where(g => g.AffiliateId == affiliateId && distinctIds.Contains(g.Id))
            .Select(g => g.Id)
            .ToListAsync();
        var invalid = distinctIds.Except(validIds).ToList();
        if (invalid.Count > 0)
            throw new InvalidOperationException("One or more modifier groups were not found for this affiliate");

        var existing = await _context.ProductModifierGroups.Where(pmg => pmg.ProductId == productId).ToListAsync();
        _context.ProductModifierGroups.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var order = 0;
        foreach (var groupId in distinctIds)
        {
            _context.ProductModifierGroups.Add(new ProductModifierGroup
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                ModifierGroupId = groupId,
                SortOrder = order++,
                CreatedAt = now,
            });
        }

        await _context.SaveChangesAsync();
        return await GetProductModifierGroupsAsync(affiliateId, productId);
    }

    public async Task<Dictionary<Guid, List<PublicModifierGroupDto>>> GetModifierGroupsForProductsAsync(Guid affiliateId, List<Guid> productIds)
    {
        if (productIds.Count == 0) return new Dictionary<Guid, List<PublicModifierGroupDto>>();

        var links = await _context.ProductModifierGroups
            .Where(pmg => productIds.Contains(pmg.ProductId))
            .Include(pmg => pmg.ModifierGroup!).ThenInclude(g => g.Options)
            .Where(pmg => pmg.ModifierGroup != null && pmg.ModifierGroup.AffiliateId == affiliateId)
            .OrderBy(pmg => pmg.SortOrder)
            .ToListAsync();

        return links
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g
                .Select(l => ToPublicDto(l.ModifierGroup!))
                .ToList());
    }

    private static ModifierGroupDto ToDto(ModifierGroup g) => new(
        g.Id, g.Name, g.NameEn, g.MinSelect, g.MaxSelect, g.Required, g.SortOrder,
        g.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Name)
            .Select(o => new ModifierOptionDto(o.Id, o.Name, o.NameEn, o.PriceDelta, o.IsDefault, o.SortOrder))
            .ToList());

    private static PublicModifierGroupDto ToPublicDto(ModifierGroup g) => new(
        g.Id, g.Name, g.MinSelect, g.MaxSelect, g.Required,
        g.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Name)
            .Select(o => new PublicModifierOptionDto(o.Id, o.Name, o.PriceDelta, o.IsDefault))
            .ToList());
}
