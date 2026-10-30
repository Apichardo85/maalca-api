using System.Text.RegularExpressions;
using Maalca.Application.Common.DTOs;
using Maalca.Domain.Enums;

namespace Maalca.Application.Common;

/// <summary>
/// Reglas compartidas del menú del día y de los flyers verticales del board (BoardLayout.Panels).
/// Las usan AffiliateService (pantalla base + contenido) y ScreenService (pantallas extra), así el
/// mismo JSON se valida igual venga de donde venga.
/// </summary>
public static class BoardPanelRules
{
    public static readonly string[] WeekDays = { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" };
    public static readonly string[] SectionKinds = { "List", "Combo", "Featured" };
    public static readonly string[] PanelKinds = { "Daily", "Ads", "Category", "Image" };
    public static readonly string[] PanelStyles = { "Modern", "Tropical" };

    /// <summary>Default cuando el negocio eligió "Flyers verticales" sin configurar los espacios.</summary>
    public static readonly IReadOnlyList<BoardPanelDto> DefaultPanels = new[]
    {
        new BoardPanelDto("Daily"),
        new BoardPanelDto("Ads"),
    };

    private const int MaxPanels = 3;
    private const int MaxSectionsPerDay = 12;
    private const int MaxItemsPerSection = 12;
    private static readonly Regex TimeFormat = new(@"^([01]\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);

    public static BoardOrientation? ParseOrientation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Enum.TryParse<BoardOrientation>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            throw new ArgumentException($"Invalid orientation '{value}'.");
        return parsed;
    }

    /// <summary>Limpia y valida la lista de flyers. null = no viene; lista vacía = "sin config" (null en BD).</summary>
    public static string? SerializePanels(List<BoardPanelDto>? panels)
    {
        if (panels is null || panels.Count == 0) return null;
        if (panels.Count > MaxPanels)
            throw new ArgumentException($"Panels: máximo {MaxPanels} flyers por pantalla.");
        var clean = new List<BoardPanelDto>();
        foreach (var p in panels)
        {
            var kind = PanelKinds.FirstOrDefault(k => string.Equals(k, p?.Kind?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Panels: tipo de flyer inválido '{p?.Kind}'.");
            string? category = null;
            if (kind == "Category")
            {
                category = p!.Category?.Trim();
                if (string.IsNullOrEmpty(category))
                    throw new ArgumentException("Panels: elige la categoría del flyer.");
                if (category.Length > 80)
                    throw new ArgumentException("Panels: el nombre de la categoría es demasiado largo.");
            }
            string? mediaUrl = null;
            if (kind == "Image")
            {
                mediaUrl = p!.MediaUrl?.Trim();
                if (string.IsNullOrEmpty(mediaUrl) || !mediaUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || mediaUrl.Length > 2000)
                    throw new ArgumentException("Panels: la imagen fija necesita una URL https válida.");
            }
            string? style = null;
            if (kind is "Daily" or "Category" && !string.IsNullOrWhiteSpace(p!.Style))
                style = PanelStyles.FirstOrDefault(s => string.Equals(s, p.Style.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"Panels: estilo inválido '{p.Style}'.");
            clean.Add(new BoardPanelDto(kind, category, mediaUrl, style));
        }
        return JsonArrayField.Serialize(clean);
    }

    public static IReadOnlyList<BoardPanelDto>? ParsePanels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var list = JsonArrayField.Parse<BoardPanelDto>(json);
        return list.Count == 0 ? null : list;
    }

    /// <summary>
    /// Valida y normaliza el menú del día. Devuelve null cuando no queda ningún día con secciones
    /// (= borrar). productIds = ids de productos del negocio (para que una sección no apunte a
    /// un plato de otro afiliado).
    /// </summary>
    public static DailyMenuDto? Normalize(DailyMenuDto menu, ISet<Guid> productIds)
    {
        var days = new Dictionary<string, List<DailyMenuSectionDto>>();
        foreach (var (rawDay, sections) in menu.Days ?? new Dictionary<string, List<DailyMenuSectionDto>>())
        {
            var day = (rawDay ?? "").Trim().ToLowerInvariant();
            if (!WeekDays.Contains(day))
                throw new ArgumentException($"DailyMenu: '{rawDay}' no es un día válido.");
            if (sections is null || sections.Count == 0) continue;
            if (sections.Count > MaxSectionsPerDay)
                throw new ArgumentException($"DailyMenu: máximo {MaxSectionsPerDay} secciones por día.");

            var clean = new List<DailyMenuSectionDto>();
            foreach (var s in sections)
            {
                var name = s?.Name?.Trim() ?? "";
                if (name.Length == 0) throw new ArgumentException($"DailyMenu ({day}): cada sección necesita un nombre.");
                if (name.Length > 60) throw new ArgumentException($"DailyMenu ({day}): '{name}' es demasiado largo (máx. 60).");
                var nameEn = Trim(s!.NameEn, 60, day);
                var kind = SectionKinds.FirstOrDefault(k => string.Equals(k, s.Kind?.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"DailyMenu ({day}): tipo de sección inválido '{s.Kind}'.");
                var items = CleanItems(s.Items, day, name);
                var itemsEn = CleanItems(s.ItemsEn, day, name);
                if (s.ProductId is Guid pid && !productIds.Contains(pid))
                    throw new ArgumentException($"DailyMenu ({day}): el plato enlazado a '{name}' no existe en tu catálogo.");
                if (kind == "Combo" && s.ProductId is null)
                    throw new ArgumentException($"DailyMenu ({day}): enlaza '{name}' a un plato para mostrar su precio.");
                var from = string.IsNullOrWhiteSpace(s.From) ? null : s.From.Trim();
                var to = string.IsNullOrWhiteSpace(s.To) ? null : s.To.Trim();
                if ((from is null) != (to is null))
                    throw new ArgumentException($"DailyMenu ({day}): '{name}' necesita hora de inicio y de fin, o ninguna.");
                if (from is not null && (!TimeFormat.IsMatch(from) || !TimeFormat.IsMatch(to!)))
                    throw new ArgumentException($"DailyMenu ({day}): horario de '{name}' debe ser HH:mm.");
                if (from is not null && from == to)
                    throw new ArgumentException($"DailyMenu ({day}): '{name}' no puede empezar y terminar a la misma hora.");
                clean.Add(new DailyMenuSectionDto(name, nameEn, kind,
                    items, itemsEn.Count == 0 ? null : itemsEn,
                    kind == "List" ? null : s.ProductId, from, to));
            }
            days[day] = clean;
        }
        if (days.Count == 0) return null;
        return new DailyMenuDto(Trim(menu.Note, 120, "note"), Trim(menu.NoteEn, 120, "note"), days);
    }

    private static List<string> CleanItems(List<string>? items, string day, string section)
    {
        var clean = (items ?? new List<string>())
            .Select(i => i?.Trim() ?? "")
            .Where(i => i.Length > 0)
            .ToList();
        if (clean.Count > MaxItemsPerSection)
            throw new ArgumentException($"DailyMenu ({day}): '{section}' tiene más de {MaxItemsPerSection} platos.");
        if (clean.Any(i => i.Length > 80))
            throw new ArgumentException($"DailyMenu ({day}): un plato de '{section}' es demasiado largo (máx. 80).");
        return clean;
    }

    private static string? Trim(string? value, int max, string where)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > max) throw new ArgumentException($"DailyMenu ({where}): texto demasiado largo (máx. {max}).");
        return v;
    }
}
