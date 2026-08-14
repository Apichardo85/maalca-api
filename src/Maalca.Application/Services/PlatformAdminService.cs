using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class PlatformAdminService : IPlatformAdminService
{
    private readonly AppDbContext _context;

    // Mantener en sync con ENTREPRENEUR_PRICE_USD en maalca-web/src/lib/plan-limits.ts — no hay
    // una única fuente de verdad compartida entre los dos repos, así que si el precio cambia
    // hay que tocar ambos lados a mano.
    private const decimal EntrepreneurPriceUsd = 38m;

    // Cuánto dura un grant de impersonation antes de expirar solo — ver UserAffiliateMap.IsImpersonation.
    private static readonly TimeSpan ImpersonationDuration = TimeSpan.FromHours(2);

    public PlatformAdminService(AppDbContext context) => _context = context;

    public async Task<bool> IsPlatformAdminAsync(string supabaseUserId, string email)
    {
        var byId = await _context.PlatformAdmins.AnyAsync(a => a.SupabaseUserId == supabaseUserId);
        if (byId) return true;

        if (string.IsNullOrEmpty(email)) return false;

        var pending = await _context.PlatformAdmins
            .FirstOrDefaultAsync(a => a.SupabaseUserId == "" && a.Email.ToLower() == email.ToLower());
        if (pending is null) return false;

        pending.SupabaseUserId = supabaseUserId;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PlatformAdminRole?> GetRoleAsync(string supabaseUserId)
    {
        var admin = await _context.PlatformAdmins.FirstOrDefaultAsync(a => a.SupabaseUserId == supabaseUserId);
        return admin?.Role;
    }

    public async Task<PlatformOpsOverviewDto> GetOverviewAsync()
    {
        var affiliates = await _context.Affiliates
            .Select(a => new { a.Plan, a.Published, a.CreatedAt })
            .ToListAsync();

        var entrepreneur = affiliates.Count(a => a.Plan == Plan.Entrepreneur);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return new PlatformOpsOverviewDto(
            TotalAffiliates: affiliates.Count,
            EntrepreneurCount: entrepreneur,
            FreeCount: affiliates.Count - entrepreneur,
            MrrUsd: entrepreneur * EntrepreneurPriceUsd,
            NewThisMonth: affiliates.Count(a => a.CreatedAt >= monthStart),
            PublishedCount: affiliates.Count(a => a.Published));
    }

    public async Task<List<PlatformAffiliateSummaryDto>> GetAffiliatesAsync()
    {
        var now = DateTime.UtcNow;
        var since30d = now.AddDays(-30);

        var affiliates = await _context.Affiliates
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var orderCounts = await _context.Orders
            .Where(o => o.CreatedAt >= since30d)
            .GroupBy(o => o.AffiliateId)
            .Select(g => new { AffiliateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AffiliateId, x => x.Count);

        var result = new List<PlatformAffiliateSummaryDto>();
        foreach (var a in affiliates)
        {
            var orders30d = orderCounts.TryGetValue(a.Id, out var c) ? c : 0;
            var alerts = new List<string>();

            if (a.Plan == Plan.Entrepreneur && !a.StripeConnectChargesEnabled)
                alerts.Add("Sin conectar pagos");
            if (a.Plan == Plan.Entrepreneur && orders30d == 0 && a.CreatedAt < now.AddDays(-30))
                alerts.Add("Sin pedidos en 30 días");
            if (!a.Published && a.CreatedAt < now.AddDays(-7))
                alerts.Add("Sin publicar");
            if (!a.IsActive)
                alerts.Add("Suspendido");
            if (a.PlanStatus == PlanStatus.PastDue)
                alerts.Add("Pago atrasado");

            result.Add(new PlatformAffiliateSummaryDto(
                a.Id, a.Name, a.Slug ?? "", a.BusinessType.ToString(), a.Plan.ToString(), a.PlanStatus.ToString(),
                a.Published, a.IsActive, a.CreatedAt, orders30d, a.StripeConnectChargesEnabled, alerts));
        }
        return result;
    }

    public async Task<PlatformAffiliateSummaryDto> SetAffiliateStatusAsync(Guid affiliateId, bool? published, bool? active)
    {
        var affiliate = await _context.Affiliates.FindAsync(affiliateId)
            ?? throw new InvalidOperationException("Ese negocio no existe.");

        if (published.HasValue) affiliate.Published = published.Value;
        if (active.HasValue) affiliate.IsActive = active.Value;
        affiliate.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var since30d = DateTime.UtcNow.AddDays(-30);
        var orders30d = await _context.Orders.CountAsync(o => o.AffiliateId == affiliateId && o.CreatedAt >= since30d);

        var alerts = new List<string>();
        var now = DateTime.UtcNow;
        if (affiliate.Plan == Plan.Entrepreneur && !affiliate.StripeConnectChargesEnabled)
            alerts.Add("Sin conectar pagos");
        if (affiliate.Plan == Plan.Entrepreneur && orders30d == 0 && affiliate.CreatedAt < now.AddDays(-30))
            alerts.Add("Sin pedidos en 30 días");
        if (!affiliate.Published && affiliate.CreatedAt < now.AddDays(-7))
            alerts.Add("Sin publicar");
        if (!affiliate.IsActive)
            alerts.Add("Suspendido");
        if (affiliate.PlanStatus == PlanStatus.PastDue)
            alerts.Add("Pago atrasado");

        return new PlatformAffiliateSummaryDto(
            affiliate.Id, affiliate.Name, affiliate.Slug ?? "", affiliate.BusinessType.ToString(),
            affiliate.Plan.ToString(), affiliate.PlanStatus.ToString(), affiliate.Published, affiliate.IsActive,
            affiliate.CreatedAt, orders30d, affiliate.StripeConnectChargesEnabled, alerts);
    }

    public async Task<ImpersonationSessionDto> StartImpersonationAsync(string adminSupabaseUserId, string adminEmail, Guid affiliateId)
    {
        var affiliate = await _context.Affiliates.FindAsync(affiliateId)
            ?? throw new InvalidOperationException("Ese negocio no existe.");

        // Limpia cualquier grant de impersonation previo de este admin antes de abrir uno nuevo —
        // solo puede haber una sesión de soporte activa a la vez.
        var stale = await _context.UserAffiliateMaps
            .Where(m => m.SupabaseUserId == adminSupabaseUserId && m.IsImpersonation)
            .ToListAsync();
        _context.UserAffiliateMaps.RemoveRange(stale);

        var expiresAt = DateTime.UtcNow.Add(ImpersonationDuration);

        // Si el admin ya es dueño/miembro real de este negocio, no hace falta (ni se debe) crear
        // un grant temporal encima — ya tiene acceso legítimo.
        var alreadyHasAccess = await _context.UserAffiliateMaps
            .AnyAsync(m => m.SupabaseUserId == adminSupabaseUserId && m.AffiliateId == affiliateId && !m.IsImpersonation);

        if (!alreadyHasAccess)
        {
            _context.UserAffiliateMaps.Add(new UserAffiliateMap
            {
                SupabaseUserId = adminSupabaseUserId,
                Email = adminEmail,
                AffiliateId = affiliateId,
                Role = AffiliateRole.Owner,
                IsImpersonation = true,
                ImpersonationExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
            });
        }

        // Auditoría permanente — independiente del grant temporal de arriba, nunca se borra.
        _context.AdminImpersonationLogs.Add(new AdminImpersonationLog
        {
            AdminSupabaseUserId = adminSupabaseUserId,
            AdminEmail = adminEmail,
            AffiliateId = affiliateId,
            StartedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync();

        return new ImpersonationSessionDto(affiliate.Id, affiliate.Slug ?? "", affiliate.Name, expiresAt);
    }

    public async Task EndImpersonationAsync(string adminSupabaseUserId)
    {
        var active = await _context.UserAffiliateMaps
            .Where(m => m.SupabaseUserId == adminSupabaseUserId && m.IsImpersonation)
            .ToListAsync();
        if (active.Count == 0) return;

        _context.UserAffiliateMaps.RemoveRange(active);

        var openLogs = await _context.AdminImpersonationLogs
            .Where(l => l.AdminSupabaseUserId == adminSupabaseUserId && l.EndedAt == null)
            .ToListAsync();
        foreach (var log in openLogs)
            log.EndedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<List<PlatformTeamMemberDto>> GetPlatformTeamAsync()
        => await _context.PlatformAdmins
            .OrderBy(a => a.CreatedAt)
            .Select(a => new PlatformTeamMemberDto(a.Id, a.Email, a.Role.ToString(), a.SupabaseUserId == "", a.CreatedAt))
            .ToListAsync();

    public async Task<PlatformTeamMemberDto> InvitePlatformAdminAsync(string email, PlatformAdminRole role)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var exists = await _context.PlatformAdmins.AnyAsync(a => a.Email.ToLower() == normalizedEmail);
        if (exists)
            throw new InvalidOperationException("Ese correo ya es parte del equipo interno.");

        // SupabaseUserId vacío = invitación pendiente, igual que UserAffiliateMap — se engancha
        // en el próximo login de esa persona (ver IsPlatformAdminAsync).
        var admin = new PlatformAdmin
        {
            SupabaseUserId = "",
            Email = normalizedEmail,
            Role = role,
            CreatedAt = DateTime.UtcNow,
        };
        _context.PlatformAdmins.Add(admin);
        await _context.SaveChangesAsync();
        return new PlatformTeamMemberDto(admin.Id, admin.Email, admin.Role.ToString(), true, admin.CreatedAt);
    }

    public async Task<PlatformTeamMemberDto> UpdatePlatformAdminRoleAsync(Guid platformAdminId, PlatformAdminRole role)
    {
        var admin = await _context.PlatformAdmins.FindAsync(platformAdminId)
            ?? throw new InvalidOperationException("Ese miembro del equipo no existe.");

        if (admin.Role == PlatformAdminRole.Owner && role != PlatformAdminRole.Owner)
            await EnsureNotLastPlatformOwnerAsync(excludingId: platformAdminId);

        admin.Role = role;
        admin.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return new PlatformTeamMemberDto(admin.Id, admin.Email, admin.Role.ToString(), admin.SupabaseUserId == "", admin.CreatedAt);
    }

    public async Task RemovePlatformAdminAsync(Guid platformAdminId)
    {
        var admin = await _context.PlatformAdmins.FindAsync(platformAdminId)
            ?? throw new InvalidOperationException("Ese miembro del equipo no existe.");

        if (admin.Role == PlatformAdminRole.Owner)
            await EnsureNotLastPlatformOwnerAsync(excludingId: platformAdminId);

        _context.PlatformAdmins.Remove(admin);
        await _context.SaveChangesAsync();
    }

    // El equipo interno siempre necesita al menos un Owner — si no, nadie podría volver a
    // gestionar el equipo ni las acciones destructivas de /ops.
    private async Task EnsureNotLastPlatformOwnerAsync(Guid excludingId)
    {
        var otherOwners = await _context.PlatformAdmins
            .CountAsync(a => a.Role == PlatformAdminRole.Owner && a.Id != excludingId);
        if (otherOwners == 0)
            throw new InvalidOperationException("No puedes quitar al último Owner del equipo interno.");
    }

    public async Task<List<AffiliateNoteDto>> GetAffiliateNotesAsync(Guid affiliateId)
        => await _context.AffiliateNotes
            .Where(n => n.AffiliateId == affiliateId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new AffiliateNoteDto(n.Id, n.AuthorEmail, n.Text, n.CreatedAt))
            .ToListAsync();

    public async Task<AffiliateNoteDto> AddAffiliateNoteAsync(Guid affiliateId, string authorEmail, string text)
    {
        var note = new AffiliateNote
        {
            AffiliateId = affiliateId,
            AuthorEmail = authorEmail,
            Text = text.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        _context.AffiliateNotes.Add(note);
        await _context.SaveChangesAsync();
        return new AffiliateNoteDto(note.Id, note.AuthorEmail, note.Text, note.CreatedAt);
    }
}
