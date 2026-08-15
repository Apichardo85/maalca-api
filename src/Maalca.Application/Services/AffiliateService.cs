using System.Text.Json;
using System.Text.RegularExpressions;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class AffiliateService : IAffiliateService
{
    private static readonly Regex TimeFormat = new(@"^([01]\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);

    private readonly AppDbContext _context;
    private readonly IPlanLimitService _planLimit;

    public AffiliateService(AppDbContext context, IPlanLimitService planLimit)
    {
        _context = context;
        _planLimit = planLimit;
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

    public async Task<AffiliatePublicProfileDto?> UpdateProfileAsync(Guid affiliateId, UpdateAffiliateProfileRequest request)
    {
        var affiliate = await _context.Affiliates.FindAsync(affiliateId);
        if (affiliate == null) return null;

        if (_planLimit.IsTrialExpired(affiliate))
            throw new InvalidOperationException(PlanLimitService.TrialExpiredMessage);

        if (request.Name != null)
        {
            if (request.Name.Length < 2 || request.Name.Length > 100)
                throw new ArgumentException("Name must be between 2 and 100 characters.");
            affiliate.Name = request.Name.Trim();
        }
        if (request.Description != null) affiliate.Description = request.Description.Trim();
        if (request.DescriptionEn != null) affiliate.DescriptionEn = request.DescriptionEn.Trim();
        if (request.LogoUrl != null) affiliate.LogoUrl = request.LogoUrl.Trim();
        if (request.CoverImageUrl != null) affiliate.CoverImageUrl = request.CoverImageUrl.Trim();
        if (request.ContactEmail != null) affiliate.ContactEmail = request.ContactEmail.Trim();
        if (request.Address != null) affiliate.Address = request.Address.Trim();
        if (request.Website != null) affiliate.Website = request.Website.Trim();
        if (request.PrimaryColor != null) affiliate.PrimaryColor = request.PrimaryColor.Trim();
        // Country solo se setea si todavía no hay una — Stripe no permite cambiar el país
        // de una cuenta conectada ya creada, así que una vez fijado no debería sobreescribirse
        // silenciosamente. Si el afiliado necesita corregirlo, es un caso manual.
        if (request.Country != null && affiliate.Country == null) affiliate.Country = request.Country.Trim().ToUpperInvariant();
        if (request.Currency != null)
        {
            var currency = request.Currency.Trim().ToUpperInvariant();
            if (currency != "USD" && currency != "DOP")
                throw new ArgumentException("Currency must be USD or DOP.");
            affiliate.Currency = currency;
        }
        if (request.AdFrequency.HasValue) affiliate.AdFrequency = request.AdFrequency.Value > 0 ? request.AdFrequency.Value : null;
        if (request.Language != null) affiliate.Language = request.Language.Trim().ToLowerInvariant();
        if (request.BoardTheme != null)
        {
            if (!Enum.TryParse<BoardTheme>(request.BoardTheme, ignoreCase: true, out var theme))
                throw new ArgumentException($"Invalid boardTheme '{request.BoardTheme}'.");
            affiliate.BoardTheme = theme;
        }
        if (request.TransitionEffect != null)
        {
            if (!Enum.TryParse<BoardTransitionEffect>(request.TransitionEffect, ignoreCase: true, out var effect))
                throw new ArgumentException($"Invalid transitionEffect '{request.TransitionEffect}'.");
            affiliate.TransitionEffect = effect;
        }

        await _context.SaveChangesAsync();

        return new AffiliatePublicProfileDto(
            affiliate.Id, affiliate.Name, affiliate.Slug!,
            affiliate.BusinessType.ToString(), affiliate.Plan.ToString(),
            affiliate.Description, affiliate.DescriptionEn, affiliate.PrimaryColor,
            affiliate.LogoUrl, affiliate.CoverImageUrl,
            affiliate.ContactEmail,
            affiliate.Address, affiliate.Website, affiliate.Country, affiliate.Currency);
    }

    public async Task<AffiliateContentDto?> UpdateContentAsync(Guid affiliateId, UpdateAffiliateContentRequest request)
    {
        var affiliate = await _context.Affiliates.FindAsync(affiliateId);
        if (affiliate == null) return null;

        if (_planLimit.IsTrialExpired(affiliate))
            throw new InvalidOperationException(PlanLimitService.TrialExpiredMessage);

        if (request.ProcessSteps != null)
        {
            foreach (var step in request.ProcessSteps)
            {
                if (string.IsNullOrWhiteSpace(step.Title))
                    throw new ArgumentException("ProcessSteps: title is required.");
                if (step.Title.Length > 200)
                    throw new ArgumentException("ProcessSteps: title must be at most 200 characters.");
                if (step.Description?.Length > 1000)
                    throw new ArgumentException("ProcessSteps: description must be at most 1000 characters.");
            }
            affiliate.ProcessSteps = JsonArrayField.Serialize(request.ProcessSteps);
        }

        if (request.Faq != null)
        {
            foreach (var item in request.Faq)
            {
                if (string.IsNullOrWhiteSpace(item.Question))
                    throw new ArgumentException("Faq: question is required.");
                if (item.Question.Length > 200)
                    throw new ArgumentException("Faq: question must be at most 200 characters.");
                if (item.Answer?.Length > 1000)
                    throw new ArgumentException("Faq: answer must be at most 1000 characters.");
            }
            affiliate.Faq = JsonArrayField.Serialize(request.Faq);
        }

        if (request.Horario != null)
        {
            foreach (var entry in request.Horario)
            {
                if (string.IsNullOrWhiteSpace(entry.Dia) || !DiaSemanaTokens.Whitelist.Contains(entry.Dia))
                    throw new ArgumentException($"Horario: '{entry.Dia}' is not a valid day.");
                if (!entry.Cerrado && (!TimeFormat.IsMatch(entry.Abre ?? "") || !TimeFormat.IsMatch(entry.Cierra ?? "")))
                    throw new ArgumentException($"Horario: Abre/Cierra must be in HH:mm format for '{entry.Dia}' when Cerrado is false.");
            }
            affiliate.Horario = JsonArrayField.Serialize(request.Horario);
        }

        await _context.SaveChangesAsync();

        return new AffiliateContentDto(
            JsonArrayField.Parse<ProcessStepDto>(affiliate.ProcessSteps),
            JsonArrayField.Parse<FaqItemDto>(affiliate.Faq),
            JsonArrayField.Parse<HorarioEntryDto>(affiliate.Horario));
    }
}
