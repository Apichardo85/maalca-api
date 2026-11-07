using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Plantilla del documento de contrato de un negocio. Es texto plano con etiquetas {{cliente}}, {{folio}}, etc.
/// que el servidor reemplaza con los datos del contrato. Una por negocio; si no hay, se usa la plantilla base.
/// </summary>
public class ContractTemplate : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Title { get; set; } = "CONTRATO";
    public string Body { get; set; } = string.Empty;

    public Affiliate? Affiliate { get; set; }
}
