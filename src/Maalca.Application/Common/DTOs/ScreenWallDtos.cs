namespace Maalca.Application.Common.DTOs;

/// <summary>Una TV del muro: qué pantalla (Screen) va en qué posición física.</summary>
public record ScreenWallSlotDto(Guid ScreenId, string ScreenName, int Col, int Row);

public record ScreenWallDto(
    Guid Id,
    string Name,
    string Mode,          // "Mirror" | "Span"
    int Cols,
    int Rows,
    int BezelPx,
    bool Calibrate,
    IReadOnlyList<ScreenWallSlotDto> Slots
);

public record CreateScreenWallRequest(
    string Name,
    string? Mode = null,
    int? Cols = null,
    int? Rows = null,
    int? BezelPx = null
);

/// <summary>Parcial: solo cambia lo que viene (null = no tocar).</summary>
public record UpdateScreenWallRequest(
    string? Name = null,
    string? Mode = null,
    int? Cols = null,
    int? Rows = null,
    int? BezelPx = null,
    bool? Calibrate = null
);

public record ScreenWallSlotInput(Guid ScreenId, int Col, int Row);

/// <summary>Reacomodo completo de las TVs del muro (intercambiar posiciones).</summary>
public record UpdateScreenWallSlotsRequest(IReadOnlyList<ScreenWallSlotInput> Slots);

/// <summary>Lo que recibe cada TV en el catálogo público: modo del muro y SU posición.</summary>
public record PublicWallDto(string Mode, int Cols, int Rows, int Col, int Row, int BezelPx, bool Calibrate);
