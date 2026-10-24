using System.Text.RegularExpressions;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>Ver IScreenDeviceService / ScreenDevice.</summary>
public class ScreenDeviceService : IScreenDeviceService
{
    public const string MainScreenName = "Pantalla principal";
    /// Señal ≤ esto = "online"; ≤ OfflineAfter = "stale"; más = "offline". El board manda cada 60 s.
    public static readonly TimeSpan OnlineWithin = TimeSpan.FromSeconds(150);
    public static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CommandTtl = TimeSpan.FromMinutes(10);
    private static readonly HashSet<string> Commands = new(StringComparer.OrdinalIgnoreCase) { "reload", "identify" };
    private static readonly HashSet<string> Kinds = new(StringComparer.OrdinalIgnoreCase) { "menu", "combo", "ad" };
    private static readonly Regex DeviceIdRx = new("^[A-Za-z0-9_-]{8,64}$", RegexOptions.Compiled);
    /// Tope de aparatos por negocio: la ruta es anónima, así nadie llena la tabla con IDs inventados.
    private const int MaxDevicesPerAffiliate = 100;

    private readonly AppDbContext _db;

    public ScreenDeviceService(AppDbContext db) => _db = db;

    public static string StatusOf(DateTime lastSeenUtc, DateTime nowUtc)
    {
        var age = nowUtc - lastSeenUtc;
        if (age <= OnlineWithin) return "online";
        if (age <= OfflineAfter) return "stale";
        return "offline";
    }

    public async Task<ScreenHeartbeatResponse?> HeartbeatAsync(string slug, ScreenHeartbeatRequest req)
    {
        var affiliateId = await _db.Affiliates.Where(a => a.Slug == slug).Select(a => (Guid?)a.Id).FirstOrDefaultAsync();
        if (affiliateId is null) return null;

        var deviceId = (req.DeviceId ?? "").Trim();
        if (!DeviceIdRx.IsMatch(deviceId)) return new ScreenHeartbeatResponse(null, null);

        string screenName = MainScreenName;
        if (req.ScreenId is Guid sid)
        {
            var name = await _db.Screens.Where(s => s.Id == sid && s.AffiliateId == affiliateId).Select(s => s.Name).FirstOrDefaultAsync();
            if (name is null) return new ScreenHeartbeatResponse(null, null); // pantalla ajena o borrada
            screenName = name;
        }

        var now = DateTime.UtcNow;
        var dev = await _db.ScreenDevices.FirstOrDefaultAsync(d =>
            d.AffiliateId == affiliateId && d.DeviceId == deviceId && d.ScreenId == req.ScreenId);
        if (dev is null)
        {
            var count = await _db.ScreenDevices.CountAsync(d => d.AffiliateId == affiliateId);
            if (count >= MaxDevicesPerAffiliate) return new ScreenHeartbeatResponse(null, screenName);
            dev = new ScreenDevice { AffiliateId = affiliateId.Value, ScreenId = req.ScreenId, DeviceId = deviceId, FirstSeenAt = now };
            _db.ScreenDevices.Add(dev);
        }

        dev.LastSeenAt = now;
        dev.UpdatedAt = now;
        dev.Visible = req.Visible;
        dev.ShowingKind = req.ShowingKind is not null && Kinds.Contains(req.ShowingKind) ? req.ShowingKind.ToLowerInvariant() : null;
        dev.ShowingLabel = Trunc(req.ShowingLabel, 200);
        dev.Viewport = Trunc(req.Viewport, 20);
        dev.UserAgent = Trunc(req.UserAgent, 300);
        dev.AppVersion = Trunc(req.AppVersion, 40);

        string? command = null;
        if (dev.PendingCommand is not null)
        {
            if (dev.CommandAt is DateTime at && now - at <= CommandTtl) command = dev.PendingCommand;
            dev.PendingCommand = null;
            dev.CommandAt = null;
        }

        await _db.SaveChangesAsync();
        return new ScreenHeartbeatResponse(command, screenName);
    }

    public async Task<List<ScreenDeviceDto>> GetForAffiliateAsync(Guid affiliateId)
    {
        var devices = await _db.ScreenDevices.Where(d => d.AffiliateId == affiliateId).ToListAsync();
        return await ToDtosAsync(devices, includeAffiliate: false);
    }

    public async Task<List<ScreenDeviceDto>> GetAllAsync()
    {
        var devices = await _db.ScreenDevices.Include(d => d.Affiliate).ToListAsync();
        return await ToDtosAsync(devices, includeAffiliate: true);
    }

    public async Task<bool> SetCommandAsync(Guid affiliateId, Guid deviceRowId, string command)
    {
        if (string.IsNullOrWhiteSpace(command) || !Commands.Contains(command)) return false;
        var dev = await _db.ScreenDevices.FirstOrDefaultAsync(d => d.Id == deviceRowId && d.AffiliateId == affiliateId);
        if (dev is null) return false;
        dev.PendingCommand = command.ToLowerInvariant();
        dev.CommandAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetArchivedAsync(Guid affiliateId, Guid deviceRowId, bool archived)
    {
        var dev = await _db.ScreenDevices.FirstOrDefaultAsync(d => d.Id == deviceRowId && d.AffiliateId == affiliateId);
        if (dev is null) return false;
        dev.Archived = archived;
        if (archived) dev.AlertSentAt = null;
        dev.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<List<ScreenDeviceDto>> ToDtosAsync(List<ScreenDevice> devices, bool includeAffiliate)
    {
        var screenIds = devices.Where(d => d.ScreenId.HasValue).Select(d => d.ScreenId!.Value).Distinct().ToList();
        var names = screenIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Screens.Where(s => screenIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
        var now = DateTime.UtcNow;
        return devices
            .Select(d => new ScreenDeviceDto(
                d.Id, d.AffiliateId,
                includeAffiliate ? d.Affiliate?.Name : null,
                includeAffiliate ? d.Affiliate?.Slug : null,
                d.ScreenId,
                d.ScreenId is Guid sid ? (names.TryGetValue(sid, out var n) ? n : "Pantalla borrada") : MainScreenName,
                d.DeviceId, StatusOf(d.LastSeenAt, now), d.FirstSeenAt, d.LastSeenAt, d.Visible,
                d.ShowingKind, d.ShowingLabel, d.Viewport, d.UserAgent, d.AppVersion,
                d.Archived, d.AlertSentAt.HasValue, d.PendingCommand))
            .OrderBy(d => d.Archived)
            .ThenBy(d => d.AffiliateName)
            .ThenBy(d => d.ScreenId.HasValue)
            .ThenBy(d => d.ScreenName)
            .ThenByDescending(d => d.LastSeenAt)
            .ToList();
    }

    private static string? Trunc(string? s, int max)
        => string.IsNullOrWhiteSpace(s) ? null : (s.Length <= max ? s.Trim() : s[..max]);
}
