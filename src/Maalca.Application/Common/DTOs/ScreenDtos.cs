namespace Maalca.Application.Common.DTOs;

public record ScreenDto(
    Guid Id,
    string Name,
    int SortOrder,
    string? Language,
    string? BoardTheme,
    int? AdFrequency,
    string? CategoryFilter
);

public record CreateScreenRequest(
    string Name,
    string? Language = null,
    string? BoardTheme = null,
    int? AdFrequency = null,
    string? CategoryFilter = null
);

/// <summary>
/// Full-state update — el form de "Pantallas" en el dashboard manda las 4 preferencias
/// completas en cada guardado (igual que savePrefs en BoardContent.tsx para el negocio),
/// así que Language/BoardTheme/AdFrequency/CategoryFilter se sobreescriben directo, sin la
/// ambigüedad de "campo ausente vs. null explícito" que sí aplica a Name/SortOrder.
/// </summary>
public record UpdateScreenRequest(
    string? Name,
    int? SortOrder,
    string? Language,
    string? BoardTheme,
    int? AdFrequency,
    string? CategoryFilter
);
