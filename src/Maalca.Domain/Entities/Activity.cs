using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Modulo de Eventos/Actividades (backlog 2026-09-25, registry.ts en maalca-web). Se dejo
/// documentado ahi que esto debia ser una entidad transversal (no exclusiva de Community) para
/// no tener que rehacer el modelo cuando otros businessType lo pidan -- por eso vive como
/// entidad propia (no como columna JSON en Affiliate, a diferencia de Causas/CommunityImpact,
/// que sí siguen ese patron porque son listas chicas que se reemplazan enteras al guardar).
/// Lanzamiento inicial: solo habilitado para businessType Community (ver SpaceSidebar /
/// ContenidoTab en maalca-web) -- el gating por tipo de negocio vive en el frontend, no aqui.
/// MVP a proposito: sin cupos/capacidad ni RSVP todavia (se decidio dejarlo para una fase
/// posterior si hace falta).
/// </summary>
public class Activity : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public string? Location { get; set; }
    public DateTime StartsAt { get; set; }
    // Null = evento sin hora de fin definida (se muestra solo la hora de inicio).
    public DateTime? EndsAt { get; set; }
    // Igual que Service.IsActive -- permite "archivar" un evento pasado sin borrarlo (historial),
    // en vez de depender solo de StartsAt < ahora para ocultarlo del publico.
    public bool IsActive { get; set; } = true;

    // Foto opcional (backlog 2026-09-26, companion del rediseno de Programas) -- igual
    // patron que CommunityProgram.ImageUrl: null = sin foto, el card publico se ve bien en
    // ambos casos (ver Community.tsx), no es obligatoria.
    public string? ImageUrl { get; set; }

    public Affiliate? Affiliate { get; set; }
}
