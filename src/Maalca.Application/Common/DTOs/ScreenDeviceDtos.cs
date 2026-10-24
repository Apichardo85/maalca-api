namespace Maalca.Application.Common.DTOs;

/// Señal de vida que manda el board cada minuto (ver ScreenDevice).
public record ScreenHeartbeatRequest(
    string DeviceId,
    Guid? ScreenId,
    bool Visible,
    string? ShowingKind,
    string? ShowingLabel,
    string? Viewport,
    string? UserAgent,
    string? AppVersion);

/// Respuesta a la señal: comando pendiente para la TV ("reload" | "identify") o null.
public record ScreenHeartbeatResponse(string? Command, string? ScreenName);

/// Estado: "online" (señal hace ≤ 2.5 min) | "stale" (≤ 5 min) | "offline" (> 5 min).
public record ScreenDeviceDto(
    Guid Id,
    Guid AffiliateId,
    string? AffiliateName,
    string? AffiliateSlug,
    Guid? ScreenId,
    string ScreenName,
    string DeviceId,
    string Status,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    bool Visible,
    string? ShowingKind,
    string? ShowingLabel,
    string? Viewport,
    string? UserAgent,
    string? AppVersion,
    bool Archived,
    bool AlertOpen,
    string? PendingCommand);

public record ScreenDeviceCommandRequest(string Command);
public record ScreenDeviceArchiveRequest(bool Archived);
