namespace Maalca.Application.Common.DTOs;

public record CanalDto(
    Guid Id,
    string Tipo,
    string Metodo,
    string ValorCrudo,
    string EnlaceGenerado,
    string? NombreVisible,
    bool Verificado,
    int Orden,
    bool Activo
);

public record CreateCanalRequest(
    string Tipo,
    string Metodo,
    string ValorCrudo,
    string? NombreVisible = null,
    int Orden = 0
);

public record UpdateCanalRequest(
    string? ValorCrudo = null,
    string? NombreVisible = null,
    int? Orden = null,
    bool? Activo = null
);
