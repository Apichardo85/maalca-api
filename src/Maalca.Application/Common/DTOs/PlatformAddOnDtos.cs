namespace Maalca.Application.Common.DTOs;

public record LocalizedTextDto(string Es, string En);

/// <summary>Extra de MaalCa tal como lo ve /servicios (solo activos, sin datos de auditoría).</summary>
public record PlatformAddOnPublicDto(
    string Key,
    LocalizedTextDto Name,
    LocalizedTextDto Desc,
    decimal Price,
    string Unit,              // "OneTime" | "Monthly" | "PerLocationMonthly" | "Block5h"
    decimal? VolumePrice,
    int? VolumeFromQty);

/// <summary>Extra de MaalCa para /ops/precios: todos los campos, incluidos los inactivos.</summary>
public record PlatformAddOnAdminDto(
    Guid Id,
    string Key,
    string NameEs,
    string NameEn,
    string DescEs,
    string DescEn,
    decimal Price,
    decimal? VolumePrice,
    int? VolumeFromQty,
    string Unit,
    int SortOrder,
    bool Active,
    DateTime? UpdatedAt,
    string? UpdatedBy);

/// <summary>Crear o editar un extra desde /ops/precios. En la edición, Key se ignora (viene en la ruta).</summary>
public record UpsertPlatformAddOnRequest(
    string? Key,
    string NameEs,
    string NameEn,
    string DescEs,
    string DescEn,
    decimal Price,
    decimal? VolumePrice,
    int? VolumeFromQty,
    string Unit,
    int SortOrder,
    bool Active);

/// <summary>Activar/desactivar el modo "Solo pantallas" de un negocio desde /ops.</summary>
public record SetScreensOnlyRequest(bool ScreensOnly);
