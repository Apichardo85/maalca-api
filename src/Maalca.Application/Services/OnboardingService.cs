using System.Text.RegularExpressions;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class OnboardingService : IOnboardingService
{
    private readonly AppDbContext _db;
    private readonly IAffiliateMapService _mapService;

    public OnboardingService(AppDbContext db, IAffiliateMapService mapService)
    {
        _db = db;
        _mapService = mapService;
    }

    public async Task<OnboardingResponse> OnboardAsync(string supabaseUserId, string email, OnboardingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Length < 2 || request.Name.Length > 100)
            throw new ArgumentException("Name must be between 2 and 100 characters.");

        var existingMaps = await _mapService.GetMapsForUserAsync(supabaseUserId);
        if (existingMaps.Count > 0)
            throw new InvalidOperationException("User already has an affiliate.");

        if (!Enum.TryParse<BusinessType>(request.BusinessType, ignoreCase: true, out var businessType))
            throw new ArgumentException($"Invalid BusinessType: {request.BusinessType}");

        var slug = await GenerateUniqueSlugAsync(request.Name);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var affiliate = new Affiliate
            {
                Name = request.Name,
                BusinessType = businessType,
                Slug = slug,
                Plan = Plan.Free,
                PlanStatus = PlanStatus.Active,
                Published = true
            };

            _db.Affiliates.Add(affiliate);
            await _db.SaveChangesAsync();

            await _mapService.CreateMapAsync(supabaseUserId, email, affiliate.Id, AffiliateRole.Owner);

            await tx.CommitAsync();

            return new OnboardingResponse(affiliate.Id, affiliate.Name, affiliate.Slug!, affiliate.BusinessType.ToString());
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task<string> GenerateUniqueSlugAsync(string name)
    {
        var baseSlug = Regex.Replace(name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');

        if (!await _db.Affiliates.AnyAsync(a => a.Slug == baseSlug))
            return baseSlug;

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!await _db.Affiliates.AnyAsync(a => a.Slug == candidate))
                return candidate;
        }
    }
}
