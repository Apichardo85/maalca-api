namespace Maalca.Application.Common.DTOs;

/// <summary>Un override de contenido del sitio: su clave y el texto en cada idioma.</summary>
public record SiteContentEntryDto(string Key, string? Es, string? En);

/// <summary>Alta o edición de un override desde /ops/sitio. Vacío en ambos = equivale a borrar.</summary>
public record UpsertSiteContentRequest(string? Es, string? En);
