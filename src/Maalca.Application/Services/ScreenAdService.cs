using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class ScreenAdService : IScreenAdService
{
    private readonly AppDbContext _db;

    public ScreenAdService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ScreenAdDto>> GetAllAsync(Guid affiliateId)
    {
        var ads = await _db.ScreenAds
            .Where(a => a.AffiliateId == affiliateId)
            .OrderBy(a => a.SortOrder)
            .ToListAsync();

        return ads.Select(ToDto).ToList();
    }

    public async Task<ScreenAdDto> CreateAsync(Guid affiliateId, CreateScreenAdRequest request)
    {
        if (!Enum.TryParse<ScreenAdMediaType>(request.MediaType, ignoreCase: true, out var mediaType))
            throw new ArgumentException($"Invalid mediaType '{request.MediaType}'.");

        var ad = new ScreenAd
        {
            AffiliateId = affiliateId,
            MediaUrl = request.MediaUrl,
            MediaType = mediaType,
            DurationSeconds = request.DurationSeconds > 0 ? request.DurationSeconds : 8,
            SortOrder = request.SortOrder,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
        };
        _db.ScreenAds.Add(ad);
        await _db.SaveChangesAsync();
        return ToDto(ad);
    }

    public async Task<ScreenAdDto?> UpdateAsync(Guid affiliateId, Guid adId, UpdateScreenAdRequest request)
    {
        var ad = await _db.ScreenAds.FirstOrDefaultAsync(a => a.Id == adId && a.AffiliateId == affiliateId);
        if (ad is null) return null;

        if (request.MediaUrl is not null) ad.MediaUrl = request.MediaUrl;
        if (request.MediaType is not null)
        {
            if (!Enum.TryParse<ScreenAdMediaType>(request.MediaType, ignoreCase: true, out var mediaType))
                throw new ArgumentException($"Invalid mediaType '{request.MediaType}'.");
            ad.MediaType = mediaType;
        }
        if (request.DurationSeconds.HasValue) ad.DurationSeconds = request.DurationSeconds.Value;
        if (request.SortOrder.HasValue) ad.SortOrder = request.SortOrder.Value;
        if (request.Active.HasValue) ad.Active = request.Active.Value;
        ad.StartsAt = request.StartsAt ?? ad.StartsAt;
        ad.EndsAt = request.EndsAt ?? ad.EndsAt;
        ad.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToDto(ad);
    }

    public async Task<bool> DeleteAsync(Guid affiliateId, Guid adId)
    {
        var ad = await _db.ScreenAds.FirstOrDefaultAsync(a => a.Id == adId && a.AffiliateId == affiliateId);
        if (ad is null) return false;
        _db.ScreenAds.Remove(ad);
        await _db.SaveChangesAsync();
        return true;
    }

    private static ScreenAdDto ToDto(ScreenAd a) => new(
        a.Id, a.MediaUrl, a.MediaType.ToString(), a.DurationSeconds, a.SortOrder, a.Active, a.StartsAt, a.EndsAt
    );
}
