using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

public class UserAffiliateMap : BaseEntity
{
    public string SupabaseUserId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public Guid AffiliateId { get; set; }
    public Affiliate Affiliate { get; set; } = null!;
    public AffiliateRole Role { get; set; }

    // ── Fase 60: panel de operaciones — impersonation de soporte ──────
    // Un mapa con IsImpersonation=true es un grant TEMPORAL creado por
    // PlatformAdminService.StartImpersonationAsync para que un admin de plataforma
    // entre al /space/{slug} de un afiliado como si fuera su Owner, reutilizando
    // TODA la autorización existente (SupabaseAuthMiddleware, /api/space/{slug},
    // /api/affiliates/by-slug/{slug}, etc.) sin tocar ninguno de esos endpoints.
    // Se excluye explícitamente de GetTeamAsync y de /api/me/affiliates para que
    // no aparezca como "miembro del equipo" ante el dueño real ni en el business
    // switcher del admin. Expira solo y se limpia perezosamente (ver GetMapsForUserAsync).
    public bool IsImpersonation { get; set; } = false;
    public DateTime? ImpersonationExpiresAt { get; set; }
}

public enum AffiliateRole { Owner = 0, Manager = 1, Staff = 2 }
