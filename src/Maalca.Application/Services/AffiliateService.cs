using System.Text.Json;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class AffiliateService : IAffiliateService
{
    private readonly AppDbContext _context;

    public AffiliateService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AffiliateDto?> GetAffiliateAsync(Guid affiliateId)
    {
        var affiliate = await _context.Affiliates.FindAsync(affiliateId);
        if (affiliate == null) return null;

        return new AffiliateDto
        {
            Id = affiliate.Id.ToString(),
            Name = affiliate.Name,
            Branding = new BrandingDto
            {
                Logo = affiliate.Logo,
                PrimaryColor = affiliate.PrimaryColor,
                SecondaryColor = affiliate.SecondaryColor,
                HeroImage = affiliate.HeroImage
            },
            Modules = string.IsNullOrEmpty(affiliate.Modules) 
                ? new List<string>() 
                : affiliate.Modules.Split(',').ToList(),
            Features = JsonSerializer.Deserialize<Dictionary<string, bool>>(affiliate.Features) ?? new(),
            Settings = JsonSerializer.Deserialize<Dictionary<string, object>>(affiliate.Settings) ?? new()
        };
    }
}
