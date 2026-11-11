using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Conexión de un negocio con un sistema externo (hoy: Clover). Capa común para POS (Clover, Toast...), redes sociales y
/// demás: las credenciales van CIFRADAS (nunca se devuelven al dashboard, solo los últimos 4) y el estado/último error
/// quedan visibles para que el dueño sepa si la conexión sigue viva. Una fila por negocio y proveedor.
/// </summary>
public class IntegrationConnection : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    // clover | toast | instagram ... (minúsculas)
    public string Provider { get; set; } = "";
    // pos | social | carrier
    public string Kind { get; set; } = "pos";
    public string? CredentialsEnc { get; set; }
    public string? CredentialsLast4 { get; set; }
    // Id del comercio en el sistema externo (Clover: merchant id) y su nombre, para confirmar que es el correcto.
    public string? ExternalId { get; set; }
    public string? ExternalName { get; set; }
    // connected | error
    public string Status { get; set; } = "connected";
    public string? LastError { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}
