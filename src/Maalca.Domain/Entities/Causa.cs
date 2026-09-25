using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Causas individuales de Community (dinero/tiempo/especie) -- movido de columna JSON en
/// Affiliate a tabla propia (backlog 2026-09-25, migracion MoveCausasToTable), mismo
/// razonamiento que Activity: permite CRUD por fila en vez de reemplazo total del array, e
/// indices/orden reales. A diferencia de Activity, esta SI tenia datos reales en produccion
/// (Affiliate.Causas), por eso la migracion incluye un paso de copia de datos ademas del
/// cambio de esquema -- ver AppDbContext y la migracion generada.
/// SortOrder reemplaza el orden implicito que tenia el array JSON (el dashboard no tiene
/// drag-and-drop, pero se preserva el orden en que el afiliado las agrego/las tenia).
/// </summary>
public class Causa : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Title { get; set; } = string.Empty;
    // "money" | "time" | "in_kind" -- validado en CausaService, mismas reglas que antes vivian
    // en AffiliateService.UpdateContentAsync.
    public string Type { get; set; } = "money";
    public string? Description { get; set; }
    // Solo aplica/se muestra si Type == "money". Null = sin meta.
    public decimal? GoalAmount { get; set; }
    public decimal? CurrentAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public Affiliate? Affiliate { get; set; }
}
