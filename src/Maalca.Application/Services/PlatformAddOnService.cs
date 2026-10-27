using System.Text.RegularExpressions;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Maalca.Application.Services;

/// <summary>
/// Extras de MaalCa (PlatformAddOns). La lectura pública se cachea 10 min en memoria y se
/// invalida al guardar desde /ops, así /servicios no golpea la base en cada visita pero un
/// cambio de precio se ve enseguida en el API (y en la web cuando revalida su propio cache).
/// </summary>
public class PlatformAddOnService : IPlatformAddOnService
{
    private const string PublicCacheKey = "platform-addons:public";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly Regex KeyPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public PlatformAddOnService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlyList<PlatformAddOnPublicDto>> GetPublicAsync()
    {
        if (_cache.TryGetValue(PublicCacheKey, out IReadOnlyList<PlatformAddOnPublicDto>? cached) && cached is not null)
            return cached;

        var rows = await _db.PlatformAddOns.AsNoTracking()
            .Where(a => a.Active)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Key)
            .ToListAsync();

        IReadOnlyList<PlatformAddOnPublicDto> result = rows.Select(a => new PlatformAddOnPublicDto(
            a.Key,
            new LocalizedTextDto(a.NameEs, a.NameEn),
            new LocalizedTextDto(a.DescEs, a.DescEn),
            a.Price,
            a.Unit.ToString(),
            a.VolumePrice,
            a.VolumeFromQty)).ToList();

        _cache.Set(PublicCacheKey, result, CacheTtl);
        return result;
    }

    public async Task<IReadOnlyList<PlatformAddOnAdminDto>> GetAllAsync()
    {
        var rows = await _db.PlatformAddOns.AsNoTracking()
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Key)
            .ToListAsync();
        return rows.Select(ToAdminDto).ToList();
    }

    public async Task<PlatformAddOnAdminDto> CreateAsync(UpsertPlatformAddOnRequest request, string? adminEmail)
    {
        var key = (request.Key ?? "").Trim().ToLowerInvariant();
        if (key.Length is < 2 or > 60 || !KeyPattern.IsMatch(key))
            throw new ArgumentException("La key debe tener entre 2 y 60 caracteres: letras minúsculas, números y guiones (ej. \"screen-kit\").");
        if (await _db.PlatformAddOns.AnyAsync(a => a.Key == key))
            throw new ArgumentException($"Ya existe un extra con la key \"{key}\".");

        var addOn = new PlatformAddOn { Key = key };
        Apply(addOn, request, adminEmail);
        _db.PlatformAddOns.Add(addOn);
        await _db.SaveChangesAsync();
        _cache.Remove(PublicCacheKey);
        return ToAdminDto(addOn);
    }

    public async Task<PlatformAddOnAdminDto?> UpdateAsync(string key, UpsertPlatformAddOnRequest request, string? adminEmail)
    {
        var addOn = await _db.PlatformAddOns.FirstOrDefaultAsync(a => a.Key == key);
        if (addOn is null) return null;

        Apply(addOn, request, adminEmail);
        addOn.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _cache.Remove(PublicCacheKey);
        return ToAdminDto(addOn);
    }

    private static void Apply(PlatformAddOn addOn, UpsertPlatformAddOnRequest r, string? adminEmail)
    {
        if (!Enum.TryParse<AddOnUnit>(r.Unit, ignoreCase: true, out var unit) || !Enum.IsDefined(unit))
            throw new ArgumentException($"Unidad inválida '{r.Unit}'. Usa OneTime, Monthly, PerLocationMonthly o Block5h.");
        if (r.Price < 0 || r.Price > 10_000)
            throw new ArgumentException("El precio debe estar entre 0 y 10,000.");
        if (r.VolumePrice is not null && (r.VolumePrice < 0 || r.VolumePrice > 10_000))
            throw new ArgumentException("El precio por volumen debe estar entre 0 y 10,000.");
        if ((r.VolumePrice is null) != (r.VolumeFromQty is null))
            throw new ArgumentException("El precio por volumen necesita las dos cosas: precio y desde qué cantidad.");
        if (r.VolumeFromQty is not null && r.VolumeFromQty < 2)
            throw new ArgumentException("El precio por volumen aplica desde la 2.ª unidad como mínimo.");

        addOn.NameEs = Required(r.NameEs, "nombre en español", 120);
        addOn.NameEn = Required(r.NameEn, "nombre en inglés", 120);
        addOn.DescEs = Required(r.DescEs, "descripción en español", 600);
        addOn.DescEn = Required(r.DescEn, "descripción en inglés", 600);
        addOn.Price = decimal.Round(r.Price, 2);
        addOn.VolumePrice = r.VolumePrice is null ? null : decimal.Round(r.VolumePrice.Value, 2);
        addOn.VolumeFromQty = r.VolumeFromQty;
        addOn.Unit = unit;
        addOn.SortOrder = r.SortOrder;
        addOn.Active = r.Active;
        addOn.UpdatedBy = string.IsNullOrWhiteSpace(adminEmail) ? addOn.UpdatedBy : adminEmail.Trim();
    }

    private static string Required(string? value, string field, int max)
    {
        var v = (value ?? "").Trim();
        if (v.Length == 0) throw new ArgumentException($"Falta el {field}.");
        if (v.Length > max) throw new ArgumentException($"El {field} no puede pasar de {max} caracteres.");
        return v;
    }

    private static PlatformAddOnAdminDto ToAdminDto(PlatformAddOn a) => new(
        a.Id, a.Key, a.NameEs, a.NameEn, a.DescEs, a.DescEn, a.Price, a.VolumePrice, a.VolumeFromQty,
        a.Unit.ToString(), a.SortOrder, a.Active, a.UpdatedAt, a.UpdatedBy);
}
