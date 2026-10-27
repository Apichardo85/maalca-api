using Maalca.Domain.Enums;

namespace Maalca.Domain.Entities;

/// <summary>
/// Plan mínimo de un módulo (token de ModuleCatalog). Editable desde /ops/modulos sin desplegar.
/// Un token sin fila usa el default de ModuleCatalog.
/// </summary>
public class ModulePlanRule
{
    public string Token { get; set; } = string.Empty;
    public PlanTier MinTier { get; set; } = PlanTier.Growth;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
