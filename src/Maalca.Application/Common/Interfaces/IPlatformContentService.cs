using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IPlatformContentService
{
    /// <summary>Todos los overrides de contenido del sitio. Cacheado; lo lee la web (pública) y /ops.</summary>
    Task<IReadOnlyList<SiteContentEntryDto>> GetAllAsync();

    /// <summary>Crea o actualiza el override de una clave. Es/En vacíos en ambos = borra el override.</summary>
    Task<SiteContentEntryDto?> UpsertAsync(string key, UpsertSiteContentRequest request, string? adminEmail);

    /// <summary>Borra el override de una clave (vuelve al texto por defecto de la web). false si no existía.</summary>
    Task<bool> DeleteAsync(string key);
}
