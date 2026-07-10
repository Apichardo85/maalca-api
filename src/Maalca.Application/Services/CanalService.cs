using System.Text.RegularExpressions;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class CanalService : ICanalService
{
    private static readonly HashSet<CanalTipo> ManualTipos = new()
    {
        CanalTipo.WhatsApp, CanalTipo.Email, CanalTipo.Telefono
    };

    private static readonly HashSet<CanalTipo> EnlaceTipos = new()
    {
        CanalTipo.Facebook, CanalTipo.Instagram, CanalTipo.TikTok
    };

    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private static readonly Regex FacebookRegex = new(
        @"^(?:https?:\/\/)?(?:www\.)?facebook\.com\/([A-Za-z0-9_.\-]+)\/?(?:\?.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex InstagramRegex = new(
        @"^(?:https?:\/\/)?(?:www\.)?instagram\.com\/([A-Za-z0-9_.]+)\/?(?:\?.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TikTokRegex = new(
        @"^(?:https?:\/\/)?(?:www\.)?tiktok\.com\/@([A-Za-z0-9_.]+)\/?(?:\?.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppDbContext _db;

    public CanalService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<CanalDto>> GetCanalesAsync(Guid affiliateId)
    {
        var canales = await _db.Canales
            .Where(c => c.AffiliateId == affiliateId)
            .OrderBy(c => c.Orden)
            .ToListAsync();

        return canales.Select(Map).ToList();
    }

    public async Task<CanalDto> CreateAsync(Guid affiliateId, CreateCanalRequest request)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException($"Affiliate {affiliateId} not found.");

        if (!Enum.TryParse<CanalTipo>(request.Tipo, ignoreCase: true, out var tipo) ||
            (!ManualTipos.Contains(tipo) && !EnlaceTipos.Contains(tipo)))
            throw new ArgumentException($"Unsupported Tipo: {request.Tipo}. Only WhatsApp, Email, Telefono, Facebook, Instagram, TikTok are supported in this phase.");

        if (!Enum.TryParse<CanalMetodo>(request.Metodo, ignoreCase: true, out var metodo))
            throw new ArgumentException($"Unsupported Metodo: {request.Metodo}.");

        if (ManualTipos.Contains(tipo) && metodo != CanalMetodo.Manual)
            throw new ArgumentException($"Unsupported Metodo: {request.Metodo}. Only Manual is supported for {tipo} in this phase.");

        if (EnlaceTipos.Contains(tipo) && metodo != CanalMetodo.Enlace)
            throw new ArgumentException($"Unsupported Metodo: {request.Metodo}. Only Enlace is supported for {tipo} in this phase.");

        if (string.IsNullOrWhiteSpace(request.ValorCrudo))
            throw new ArgumentException("ValorCrudo is required.");

        var canal = new Canal
        {
            AffiliateId = affiliateId,
            Tipo = tipo,
            Metodo = metodo,
            ValorCrudo = request.ValorCrudo,
            EnlaceGenerado = GenerarEnlace(tipo, request.ValorCrudo),
            NombreVisible = request.NombreVisible,
            Orden = request.Orden,
            Activo = true,
            Verificado = false
        };

        _db.Canales.Add(canal);
        await _db.SaveChangesAsync();
        return Map(canal);
    }

    public async Task<CanalDto?> UpdateAsync(Guid affiliateId, Guid canalId, UpdateCanalRequest request)
    {
        var canal = await _db.Canales
            .FirstOrDefaultAsync(c => c.Id == canalId && c.AffiliateId == affiliateId);
        if (canal == null) return null;

        if (request.ValorCrudo != null)
        {
            if (string.IsNullOrWhiteSpace(request.ValorCrudo))
                throw new ArgumentException("ValorCrudo is required.");
            canal.ValorCrudo = request.ValorCrudo;
            canal.EnlaceGenerado = GenerarEnlace(canal.Tipo, request.ValorCrudo);
        }
        if (request.NombreVisible != null) canal.NombreVisible = request.NombreVisible;
        if (request.Orden.HasValue) canal.Orden = request.Orden.Value;
        if (request.Activo.HasValue) canal.Activo = request.Activo.Value;

        await _db.SaveChangesAsync();
        return Map(canal);
    }

    public async Task<bool> DeleteAsync(Guid affiliateId, Guid canalId)
    {
        var canal = await _db.Canales
            .FirstOrDefaultAsync(c => c.Id == canalId && c.AffiliateId == affiliateId);
        if (canal == null) return false;

        _db.Canales.Remove(canal);
        await _db.SaveChangesAsync();
        return true;
    }

    private static string GenerarEnlace(CanalTipo tipo, string valorCrudo) => tipo switch
    {
        CanalTipo.WhatsApp => BuildWhatsAppLink(valorCrudo),
        CanalTipo.Email => BuildEmailLink(valorCrudo),
        CanalTipo.Telefono => BuildPhoneLink(valorCrudo),
        CanalTipo.Facebook => BuildFacebookLink(valorCrudo),
        CanalTipo.Instagram => BuildInstagramLink(valorCrudo),
        CanalTipo.TikTok => BuildTikTokLink(valorCrudo),
        _ => throw new ArgumentException($"Cannot generate link for Tipo {tipo} in this phase.")
    };

    private static string BuildWhatsAppLink(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length < 7)
            throw new ArgumentException("WhatsApp number must have at least 7 digits.");
        return $"https://wa.me/{digits}";
    }

    private static string BuildEmailLink(string raw)
    {
        if (!EmailRegex.IsMatch(raw))
            throw new ArgumentException("Invalid email format.");
        return $"mailto:{raw}";
    }

    private static string BuildPhoneLink(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length < 7)
            throw new ArgumentException("Phone number must have at least 7 digits.");
        var prefix = raw.TrimStart().StartsWith("+") ? "+" : "";
        return $"tel:{prefix}{digits}";
    }

    private static string BuildFacebookLink(string raw)
    {
        var match = FacebookRegex.Match(raw.Trim());
        if (!match.Success)
            throw new ArgumentException("Ese no parece un link de Facebook válido.");
        return $"https://facebook.com/{match.Groups[1].Value}";
    }

    private static string BuildInstagramLink(string raw)
    {
        var match = InstagramRegex.Match(raw.Trim());
        if (!match.Success)
            throw new ArgumentException("Ese no parece un link de Instagram válido.");
        return $"https://instagram.com/{match.Groups[1].Value}";
    }

    private static string BuildTikTokLink(string raw)
    {
        var match = TikTokRegex.Match(raw.Trim());
        if (!match.Success)
            throw new ArgumentException("Ese no parece un link de TikTok válido.");
        return $"https://tiktok.com/@{match.Groups[1].Value}";
    }

    private static CanalDto Map(Canal c) => new(
        c.Id, c.Tipo.ToString(), c.Metodo.ToString(), c.ValorCrudo, c.EnlaceGenerado,
        c.NombreVisible, c.Verificado, c.Orden, c.Activo);
}
