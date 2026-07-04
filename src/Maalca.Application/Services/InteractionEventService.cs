using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class InteractionEventService : IInteractionEventService
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        InteractionEventKeys.QrScan, InteractionEventKeys.CanalClick, InteractionEventKeys.PageView
    };

    private readonly AppDbContext _db;

    public InteractionEventService(AppDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(Guid affiliateId, string type, Guid? canalId)
    {
        if (!AllowedTypes.Contains(type))
            throw new ArgumentException($"Unsupported event type: {type}");

        var affiliateExists = await _db.Affiliates.AnyAsync(a => a.Id == affiliateId);
        if (!affiliateExists)
            throw new KeyNotFoundException($"Affiliate {affiliateId} not found.");

        if (canalId.HasValue)
        {
            var canalBelongsToAffiliate = await _db.Canales
                .AnyAsync(c => c.Id == canalId.Value && c.AffiliateId == affiliateId);
            if (!canalBelongsToAffiliate)
                throw new ArgumentException("CanalId does not belong to this affiliate.");
        }

        var tipo = type.ToLowerInvariant() switch
        {
            InteractionEventKeys.QrScan => EventoTipo.QrScan,
            InteractionEventKeys.CanalClick => EventoTipo.CanalClick,
            InteractionEventKeys.PageView => EventoTipo.PageView,
            _ => throw new ArgumentException($"Unsupported event type: {type}")
        };

        _db.EventosInteraccion.Add(new EventoInteraccion
        {
            AffiliateId = affiliateId,
            Tipo = tipo,
            CanalId = canalId
        });

        await _db.SaveChangesAsync();
    }
}
