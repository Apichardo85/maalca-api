using Maalca.Domain.Common;
using Maalca.Domain.Enums;

namespace Maalca.Domain.Entities;

public class Canal : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public CanalTipo Tipo { get; set; }
    public CanalMetodo Metodo { get; set; }
    public string ValorCrudo { get; set; } = string.Empty;
    public string EnlaceGenerado { get; set; } = string.Empty;
    public string? NombreVisible { get; set; }
    public bool Verificado { get; set; } = false;
    public string? OauthRef { get; set; }
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;

    public Affiliate? Affiliate { get; set; }
}
