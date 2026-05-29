using System.Text.Json;
using System.Text.RegularExpressions;
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

    public async Task<UpdateProfileResult?> UpdateProfileAsync(Guid affiliateId, UpdateAffiliateProfileRequest request)
    {
        var affiliate = await _context.Affiliates.FindAsync(affiliateId);
        if (affiliate == null) return null;

        if (request.WhatsApp != null && !IsValidPhone(request.WhatsApp))
            throw new ArgumentException("WhatsApp must be a valid phone number (7–15 digits).");

        var hadWhatsApp = !string.IsNullOrWhiteSpace(affiliate.WhatsApp);

        if (request.Name != null)
        {
            if (request.Name.Length < 2 || request.Name.Length > 100)
                throw new ArgumentException("Name must be between 2 and 100 characters.");
            affiliate.Name = request.Name.Trim();
        }
        if (request.Description != null) affiliate.Description = request.Description.Trim();
        if (request.WhatsApp != null) affiliate.WhatsApp = request.WhatsApp.Trim();
        if (request.LogoUrl != null) affiliate.LogoUrl = request.LogoUrl.Trim();
        if (request.CoverImageUrl != null) affiliate.CoverImageUrl = request.CoverImageUrl.Trim();
        if (request.ContactEmail != null) affiliate.ContactEmail = request.ContactEmail.Trim();
        if (request.Address != null) affiliate.Address = request.Address.Trim();
        if (request.Website != null) affiliate.Website = request.Website.Trim();
        if (request.PrimaryColor != null) affiliate.PrimaryColor = request.PrimaryColor.Trim();

        await _context.SaveChangesAsync();

        var nowHasWhatsApp = !string.IsNullOrWhiteSpace(affiliate.WhatsApp);
        var profile = new AffiliatePublicProfileDto(
            affiliate.Id, affiliate.Name, affiliate.Slug!,
            affiliate.BusinessType.ToString(), affiliate.Plan.ToString(),
            affiliate.Description, affiliate.PrimaryColor,
            affiliate.LogoUrl, affiliate.CoverImageUrl,
            affiliate.WhatsApp, affiliate.ContactEmail,
            affiliate.Address, affiliate.Website);

        return new UpdateProfileResult(profile, WhatsAppWasJustConfigured: !hadWhatsApp && nowHasWhatsApp);
    }

    private static bool IsValidPhone(string phone)
    {
        var digits = Regex.Replace(phone, @"[^\d]", "");
        return digits.Length >= 7 && digits.Length <= 15;
    }
}
