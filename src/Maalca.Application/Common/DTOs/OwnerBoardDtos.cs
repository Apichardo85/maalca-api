namespace Maalca.Application.Common.DTOs;

public record BoardCollectedDto(decimal Total, decimal Cash, decimal Transfer, int Count);

public record BoardDeliveriesDto(int Count, int FullDelivered, int EmptiesReturned, int Damaged, int StopsNotHome);

/// <summary>Lo que pasó hoy (día del negocio, RD UTC-4) para el tablero del dueño. Lo demás (camiones en vivo, saldos, anaqueles) sale de endpoints que ya existen.</summary>
public record OwnerBoardTodayDto(DateOnly Date, BoardCollectedDto Collected, BoardDeliveriesDto Deliveries);
