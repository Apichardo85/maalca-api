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

    // Owner: control total (incluye publicar/pausar negocios y gestionar este mismo equipo).
    // Support: mismo acceso de lectura + impersonation para dar soporte, pero no puede tocar
    // acciones destructivas de negocio ni el equipo interno — ver comentario de gating en
    // PlatformAdminService y los checks de "platform_role" en Program.cs.
    public PlatformAdminRole Role { get; set; } = PlatformAdminRole.Owner;
}

public enum PlatformAdminRole { Owner = 0, Support = 1 }

/// <summary>
/// Nota interna de CRM sobre un afiliado — visible solo en /ops, nunca al dueño del negocio.
/// Simple bitácora cronológica (sin edición/borrado en v1) para que el equipo de MaalCa lleve
/// contexto de la relación con cada negocio sin depender de memoria o chats sueltos.
/// </summary>
public class AffiliateNote : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public string AuthorEmail { get; set; } = null!;
    public string Text { get; set; } = null!;
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
