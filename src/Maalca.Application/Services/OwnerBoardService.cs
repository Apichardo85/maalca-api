using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class OwnerBoardService : IOwnerBoardService
{
    // República Dominicana: UTC-4 todo el año (sin horario de verano). Igual que "hoy" en la web del negocio.
    private static readonly TimeSpan BusinessOffset = TimeSpan.FromHours(-4);

    private readonly AppDbContext _db;

    public OwnerBoardService(AppDbContext db) => _db = db;

    public async Task<OwnerBoardTodayDto> GetTodayAsync(Guid affiliateId)
    {
        var localNow = DateTime.UtcNow + BusinessOffset;
        var today = DateOnly.FromDateTime(localNow);
        // Límites del día del negocio expresados en UTC.
        var fromUtc = DateTime.SpecifyKind(localNow.Date - BusinessOffset, DateTimeKind.Utc);
        var toUtc = fromUtc.AddDays(1);

        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.AffiliateId == affiliateId && p.PaidAt >= fromUtc && p.PaidAt < toUtc)
            .Select(p => new { p.Amount, p.Method }).ToListAsync();

        var movements = await _db.ContainerMovements.AsNoTracking()
            .Where(m => m.AffiliateId == affiliateId && m.OccurredAt >= fromUtc && m.OccurredAt < toUtc && m.DeliveryId != null)
            .GroupBy(m => m.Type)
            .Select(g => new { Type = g.Key, Qty = g.Sum(m => m.Quantity) }).ToListAsync();
        int Qty(string type) => movements.FirstOrDefault(m => m.Type == type)?.Qty ?? 0;

        var deliveries = await _db.Deliveries.AsNoTracking()
            .CountAsync(d => d.AffiliateId == affiliateId && d.DeliveredAt >= fromUtc && d.DeliveredAt < toUtc);
        var notHome = await _db.RouteStops.AsNoTracking()
            .CountAsync(s => s.AffiliateId == affiliateId && s.Status == "failed" && s.VisitedAt >= fromUtc && s.VisitedAt < toUtc);

        return new OwnerBoardTodayDto(
            today,
            new BoardCollectedDto(
                payments.Sum(p => p.Amount),
                payments.Where(p => p.Method == "cash").Sum(p => p.Amount),
                payments.Where(p => p.Method == "transfer").Sum(p => p.Amount),
                payments.Count),
            new BoardDeliveriesDto(deliveries, Qty("delivery"), Qty("return"), Qty("damaged"), notHome));
    }
}
