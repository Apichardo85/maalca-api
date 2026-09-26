using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Rediseño del modulo "Programas" de Community (backlog 2026-09-25, ver TODO que reemplaza
/// en registry.ts). Antes reutilizaba la tabla Service del catalogo generico (Precio
/// relabeled a "Meta") -- un parche, no un modelo real: un programa comunitario no vende un
/// servicio con duracion/modalidad, tiene cupos, horario, dias de la semana y a veces necesita
/// voluntarios. Entidad propia, mismo patron que Activity/Causa (no vive en Affiliate como
/// columna JSON, tiene su propio CRUD /space/[slug]/programs). Nombrada "CommunityProgram" en
/// vez de "Program" a proposito -- Maalca.Api/Program.cs (top-level statements) ya genera una
/// clase implicita "Program" en el namespace global, así que "Program" a secas hubiera chocado
/// ahí apenas se importara Maalca.Domain.Entities sin calificar.
/// </summary>
public class CommunityProgram : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public string? ImageUrl { get; set; }

    // "Meta" opcional -- un programa comunitario no vende a precio fijo, pero puede tener una
    // meta de recaudacion/aporte asociada (igual semántica que el Price->Meta relabeled de
    // antes, pero como campo propio en vez de un Price prestado).
    public decimal? GoalAmount { get; set; }

    // Cupos disponibles -- null = sin limite / no aplica (ej. un programa de entrega continua
    // sin registro por cupo).
    public int? Capacity { get; set; }

    // Horario en texto libre (ej. "9:00am - 11:00am") -- se evaluo modelarlo como hora
    // estructurada pero un programa recurrente semanal no tiene una unica fecha/hora como
    // Activity (StartsAt/EndsAt); texto libre alcanza para el MVP y evita reinventar RRULE.
    public string? Schedule { get; set; }

    // CSV de dias de la semana -- mismo patron/formato que Product.WeekDays (ver TokenList.cs),
    // reutiliza el mismo WeekDayEditor del frontend en vez de inventar otro formato.
    public string? WeekDays { get; set; }

    // Voluntarios requeridos para este programa -- null = no aplica/no requiere voluntarios.
    public int? VolunteersNeeded { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public Affiliate? Affiliate { get; set; }
}
