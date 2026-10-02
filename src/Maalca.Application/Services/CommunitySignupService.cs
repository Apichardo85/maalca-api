using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>
/// Inscripciones de Comunidad (voluntarios y eventos con cupo). Ver CommunitySignup.cs.
/// El alta pública resuelve el negocio por slug + Published, valida con ArgumentException y es
/// idempotente frente al doble envío (misma persona + mismo destino activo = rechazo).
/// </summary>
public class CommunitySignupService : ICommunitySignupService
{
    private const int MaxPartySize = 10;
    private static readonly string[] ValidStatuses = { "New", "Confirmed", "Cancelled" };

    private readonly AppDbContext _db;
    private readonly ICommunitySignupNotificationService _notifications;

    public CommunitySignupService(AppDbContext db, ICommunitySignupNotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<PublicSignupResultDto> CreatePublicAsync(string affiliateSlug, CreatePublicSignupRequest request)
    {
        var affiliate = await _db.Affiliates.FirstOrDefaultAsync(a => a.Slug == affiliateSlug && a.Published);
        if (affiliate is null)
            throw new KeyNotFoundException();
        if (affiliate.BusinessType != BusinessType.Community)
            throw new ArgumentException("Este negocio no acepta inscripciones.");

        var kind = (request.Kind ?? string.Empty).Trim().ToLowerInvariant();
        if (kind != "volunteer" && kind != "event")
            throw new ArgumentException("Tipo de inscripción no válido.");

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new ArgumentException("El nombre es requerido.");
        if (name.Length > 100)
            throw new ArgumentException("El nombre es demasiado largo.");

        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        if (phone is null && email is null)
            throw new ArgumentException("Deja un teléfono o un correo para poder contactarte.");
        if (phone is not null && (phone.Length > 30 || !PhoneRules.IsValid(phone)))
            throw new ArgumentException("El teléfono no es válido: escribe 10 dígitos.");
        if (email is not null && (email.Length > 200 || !email.Contains('@') || email.Contains(' ')))
            throw new ArgumentException("El correo no es válido.");

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (notes is not null && notes.Length > 500)
            throw new ArgumentException("El mensaje es demasiado largo (máximo 500 caracteres).");

        var language = string.Equals(request.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";

        Guid? causaId = null;
        Guid? activityId = null;
        Maalca.Domain.Entities.Activity? activity = null;
        string title;
        int partySize = 1;
        string status;

        if (kind == "volunteer")
        {
            if (request.CausaId is not Guid cid)
                throw new ArgumentException("Elige a qué quieres ayudar.");
            var causa = await _db.Causas.FirstOrDefaultAsync(c =>
                c.Id == cid && c.AffiliateId == affiliate.Id && c.IsActive && c.Type == "time");
            if (causa is null)
                throw new ArgumentException("Esa causa ya no está disponible.");
            causaId = causa.Id;
            title = causa.Title;
            status = "New";
        }
        else
        {
            if (request.ActivityId is not Guid aid)
                throw new ArgumentException("Elige un evento.");
            activity = await _db.Activities.FirstOrDefaultAsync(a =>
                a.Id == aid && a.AffiliateId == affiliate.Id && a.IsActive);
            if (activity is null)
                throw new ArgumentException("Ese evento ya no está disponible.");
            if ((activity.EndsAt ?? activity.StartsAt) < DateTime.UtcNow)
                throw new ArgumentException("Ese evento ya pasó.");

            partySize = request.PartySize ?? 1;
            if (partySize < 1)
                throw new ArgumentException("El número de personas debe ser al menos 1.");
            if (partySize > MaxPartySize)
                throw new ArgumentException($"Para grupos de más de {MaxPartySize} personas, contacta directamente al negocio.");

            if (activity.Capacity is int capacity)
            {
                var taken = (await GetTakenByActivityAsync(new[] { activity.Id })).GetValueOrDefault(activity.Id);
                var left = capacity - taken;
                if (left <= 0)
                    throw new ArgumentException("Este evento ya no tiene lugares disponibles.");
                if (partySize > left)
                    throw new ArgumentException($"Solo quedan {left} lugares en este evento.");
            }

            activityId = activity.Id;
            title = activity.Title;
            status = "Confirmed";
        }

        // Doble envío / misma persona anotándose dos veces al mismo destino.
        var duplicate = await _db.CommunitySignups.AnyAsync(s =>
            s.AffiliateId == affiliate.Id && s.Kind == kind
            && s.CausaId == causaId && s.ActivityId == activityId
            && s.Status != "Cancelled"
            && ((phone != null && s.Phone == phone) || (email != null && s.Email == email)));
        if (duplicate)
            throw new ArgumentException("Ya estás inscrito/a aquí.");

        var signup = new CommunitySignup
        {
            Id = Guid.NewGuid(),
            AffiliateId = affiliate.Id,
            Kind = kind,
            CausaId = causaId,
            ActivityId = activityId,
            TargetTitle = title.Length > 200 ? title[..200] : title,
            Name = name,
            Phone = phone,
            Email = email,
            PartySize = partySize,
            Notes = notes,
            Language = language,
            Status = status,
            CreatedAt = DateTime.UtcNow,
        };
        _db.CommunitySignups.Add(signup);
        await _db.SaveChangesAsync();

        await _notifications.NotifySignupCreatedAsync(signup, affiliate, activity);

        return new PublicSignupResultDto(signup.Id, signup.Kind, signup.Status, signup.TargetTitle, signup.PartySize);
    }

    public async Task<List<CommunitySignup>> ListAsync(Guid affiliateId, string? kind, string? status)
    {
        var query = _db.CommunitySignups.AsNoTracking().Where(s => s.AffiliateId == affiliateId);
        if (!string.IsNullOrWhiteSpace(kind))
        {
            var k = kind.Trim().ToLowerInvariant();
            query = query.Where(s => s.Kind == k);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(s => s.Status == st);
        }
        return await query.OrderByDescending(s => s.CreatedAt).Take(500).ToListAsync();
    }

    public async Task<CommunitySignup?> UpdateStatusAsync(Guid affiliateId, Guid id, string status)
    {
        var next = ValidStatuses.FirstOrDefault(v => string.Equals(v, status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (next is null)
            throw new ArgumentException("Estado no válido.");

        var signup = await _db.CommunitySignups.FirstOrDefaultAsync(s => s.Id == id && s.AffiliateId == affiliateId);
        if (signup is null) return null;
        if (signup.Status == next) return signup;

        Maalca.Domain.Entities.Activity? activity = null;
        if (signup.ActivityId is Guid aid)
            activity = await _db.Activities.FirstOrDefaultAsync(a => a.Id == aid && a.AffiliateId == affiliateId);

        // Reactivar una inscripción cancelada vuelve a ocupar cupo: debe seguir cabiendo.
        if (signup.Status == "Cancelled" && activity?.Capacity is int capacity)
        {
            var taken = (await GetTakenByActivityAsync(new[] { activity.Id })).GetValueOrDefault(activity.Id);
            if (taken + signup.PartySize > capacity)
                throw new InvalidOperationException("Ya no hay lugares suficientes en este evento.");
        }

        signup.Status = next;
        signup.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var affiliate = await _db.Affiliates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == affiliateId);
        if (affiliate is not null)
        {
            if (next == "Cancelled")
                await _notifications.NotifySignupStatusAsync(signup, affiliate, activity, "cancelled");
            else if (next == "Confirmed" && signup.Kind == "volunteer")
                await _notifications.NotifySignupStatusAsync(signup, affiliate, activity, "confirmed");
        }

        return signup;
    }

    public async Task<bool> DeleteAsync(Guid affiliateId, Guid id)
    {
        var signup = await _db.CommunitySignups.FirstOrDefaultAsync(s => s.Id == id && s.AffiliateId == affiliateId);
        if (signup is null) return false;
        _db.CommunitySignups.Remove(signup);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<Dictionary<Guid, int>> GetTakenByActivityAsync(IEnumerable<Guid> activityIds)
    {
        var ids = activityIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();
        var rows = await _db.CommunitySignups
            .AsNoTracking()
            .Where(s => s.ActivityId != null && ids.Contains(s.ActivityId.Value) && s.Status != "Cancelled")
            .GroupBy(s => s.ActivityId!.Value)
            .Select(g => new { ActivityId = g.Key, Taken = g.Sum(x => x.PartySize) })
            .ToListAsync();
        return rows.ToDictionary(r => r.ActivityId, r => r.Taken);
    }
}
