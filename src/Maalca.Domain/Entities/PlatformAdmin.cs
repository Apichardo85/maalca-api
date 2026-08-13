using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Allowlist de dueños/operadores de MaalCa (no de un afiliado — plataforma entera).
/// Mismo patrón invite-claim que UserAffiliateMap: se siembra con SupabaseUserId="" y un
/// Email conocido; se "reclama" (backfill de SupabaseUserId) la primera vez que esa persona
/// inicia sesión con ese correo verificado — ver PlatformAdminService.IsPlatformAdminAsync.
/// </summary>
public class PlatformAdmin : BaseEntity
{
    public string SupabaseUserId { get; set; } = "";
    public string Email { get; set; } = null!;
}

/// <summary>
/// Registro inmutable de auditoría de cada sesión de impersonation (soporte). Se escribe una
/// fila al iniciar; nunca se edita ni se borra — es el rastro permanente de "quién entró a
/// qué negocio y cuándo", independiente del grant vivo y temporal en UserAffiliateMap
/// (IsImpersonation=true), que sí se limpia al expirar.
/// </summary>
public class AdminImpersonationLog : BaseEntity
{
    public string AdminSupabaseUserId { get; set; } = null!;
    public string AdminEmail { get; set; } = null!;
    public Guid AffiliateId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
}
