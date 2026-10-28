using System.Text.RegularExpressions;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Maalca.Application.Services;

/// <summary>
/// Overrides de contenido del sitio de MaalCa (PlatformContent). La lectura se cachea 5 min y se
/// invalida al guardar desde /ops/sitio, así la web no golpea la base en cada visita pero un
/// cambio de copy se ve enseguida. Sin filas = la web usa sus textos por defecto.
/// </summary>
public class PlatformContentService : IPlatformContentService
{
    private const string CacheKey = "platform-content:all";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    // Clave de traducción: minúsculas, dígitos y puntos (ej. "home.hero.title1"). Igual que la web.
    private static readonly Regex KeyPattern = new(@"^[a-z0-9]+(\.[a-z0-9]+)*$", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public PlatformContentService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlyList<SiteContentEntryDto>> GetAllAsync()
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<SiteContentEntryDto>? cached) && cached is not null)
            return cached;

        var rows = await _db.PlatformContents.AsNoTracking()
            .OrderBy(c => c.Key)
            .Select(c => new SiteContentEntryDto(c.Key, c.Es, c.En))
            .ToListAsync();

        _cache.Set(CacheKey, (IReadOnlyList<SiteContentEntryDto>)rows, CacheTtl);
        return rows;
    }

    public async Task<SiteContentEntryDto?> UpsertAsync(string key, UpsertSiteContentRequest request, string? adminEmail)
    {
        key = (key ?? string.Empty).Trim();
        if (!KeyPattern.IsMatch(key))
            throw new ArgumentException("La clave debe ser una clave de traducción (ej. home.hero.title1).");

        // El saneo del texto lo hace la web al mostrar; acá es copy de marketing, no HTML del dueño.
        var es = string.IsNullOrWhiteSpace(request.Es) ? null : request.Es.Trim();
        var en = string.IsNullOrWhiteSpace(request.En) ? null : request.En.Trim();

        var existing = await _db.PlatformContents.FirstOrDefaultAsync(c => c.Key == key);

        // Vacío en ambos idiomas = volver al texto por defecto: si existía, se borra el override.
        if (es is null && en is null)
        {
            if (existing is not null)
            {
                _db.PlatformContents.Remove(existing);
                await _db.SaveChangesAsync();
                InvalidateCache();
            }
            return null;
        }

        if (existing is null)
        {
            existing = new PlatformContent { Key = key };
            _db.PlatformContents.Add(existing);
        }
        existing.Es = es;
        existing.En = en;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = adminEmail;
        await _db.SaveChangesAsync();
        InvalidateCache();

        return new SiteContentEntryDto(existing.Key, existing.Es, existing.En);
    }

    public async Task<bool> DeleteAsync(string key)
    {
        key = (key ?? string.Empty).Trim();
        var existing = await _db.PlatformContents.FirstOrDefaultAsync(c => c.Key == key);
        if (existing is null) return false;

        _db.PlatformContents.Remove(existing);
        await _db.SaveChangesAsync();
        InvalidateCache();
        return true;
    }

    private void InvalidateCache() => _cache.Remove(CacheKey);
}
