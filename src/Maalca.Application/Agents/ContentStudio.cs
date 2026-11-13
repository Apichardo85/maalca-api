using System.Globalization;
using System.Text.RegularExpressions;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Agents;

// ---------- DTOs de /ops/contenido ----------

public record ContentItemDto(Guid Id, Guid AffiliateId, string AffiliateName, string PlanMonth, DateTime ScheduledFor,
    string Network, string? Occasion, string Title, string Caption, string? Hashtags, string MediaKind, string? MediaBrief,
    string? MediaUrl, string Status, string Source, string? ReviewVerdict, string? ReviewNotes, DateTime? ReviewedAt,
    string? StaffNote, string? DecidedBy, DateTime? DecidedAt, DateTime CreatedAt);

public record ContentQuotaDto(Guid AffiliateId, string PlanMonth, int PostsPerMonth, List<string> Networks, int Used, int Remaining,
    int Drafts, int Approved, int Published);

public record UpdateContentItemRequest(string? Network, string? Date, string? Occasion, string? Title, string? Caption,
    string? Hashtags, string? MediaKind, string? MediaBrief, string? MediaUrl);

public record CreateContentItemRequest(Guid AffiliateId, string Network, string Date, string? Occasion, string Title,
    string Caption, string? Hashtags, string? MediaKind, string? MediaBrief, string? MediaUrl);

/// <param name="Decision">"approve", "reject" o "request_changes".</param>
public record ContentDecisionRequest(string Decision, string? Note);

public record RequestPlanRequest(Guid AffiliateId, string Month, string? Instructions);

public record RequestReviewRequest(Guid AffiliateId, string Month);

public record AgentJobTicketDto(Guid JobId);

public interface IContentStudioService
{
    Task<List<ContentItemDto>> ListAsync(Guid? affiliateId, string? month, string? status, int limit);
    Task<ContentQuotaDto> GetQuotaAsync(Guid affiliateId, string month);
    Task<ContentItemDto> CreateAsync(CreateContentItemRequest request, string? by);
    Task<ContentItemDto> UpdateAsync(Guid id, UpdateContentItemRequest request, string? by);
    Task<ContentItemDto> DecideAsync(Guid id, ContentDecisionRequest request, string? by);
    Task<AgentJobTicketDto> RequestPlanAsync(RequestPlanRequest request, string? by);
    Task<AgentJobTicketDto> RequestReviewAsync(RequestReviewRequest request, string? by);
}

/// <summary>Reglas del estudio de contenido que cumplen por igual el staff y las herramientas de los agentes.</summary>
public static class ContentRules
{
    public const int MaxCaption = 2200;
    public const int MaxHashtags = 600;
    public const int MaxTitle = 160;
    public static readonly string[] MediaKinds = { "none", "image", "video" };
    private static readonly Regex MonthPattern = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    public static bool IsMonth(string? value) => value is not null && MonthPattern.IsMatch(value);

    public static string MonthOf(DateTime date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>Convierte "yyyy-MM-dd" a mediodía UTC (evita que una zona horaria mueva el día).</summary>
    public static bool TryParseDate(string? value, out DateTime date)
    {
        date = default;
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return false;
        date = new DateTime(d.Year, d.Month, d.Day, 12, 0, 0, DateTimeKind.Utc);
        return true;
    }

    /// <summary>Piezas que cuentan contra la cuota del mes: todas menos las rechazadas.</summary>
    public static Task<int> CountUsedAsync(AppDbContext db, Guid affiliateId, string month) =>
        db.ContentItems.CountAsync(c => c.AffiliateId == affiliateId && c.PlanMonth == month && c.Status != ContentStatus.Rejected);

    public static List<string> SplitNetworks(string networks) =>
        networks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>Valida los campos de una pieza. Devuelve el mensaje de error o null si está bien.</summary>
    public static string? Validate(string network, string title, string caption, string? hashtags, string mediaKind, string[] planNetworks)
    {
        if (!planNetworks.Contains(network)) return $"La red \"{network}\" no está incluida en el plan de este negocio.";
        if (string.IsNullOrWhiteSpace(title)) return "Falta el título.";
        if (title.Length > MaxTitle) return $"El título no puede pasar de {MaxTitle} caracteres.";
        if (string.IsNullOrWhiteSpace(caption)) return "Falta el texto de la publicación.";
        if (caption.Length > MaxCaption) return $"El texto no puede pasar de {MaxCaption} caracteres.";
        if (hashtags is { Length: > MaxHashtags }) return $"Los hashtags no pueden pasar de {MaxHashtags} caracteres.";
        if (!MediaKinds.Contains(mediaKind)) return "El tipo de medio debe ser none, image o video.";
        return null;
    }
}

public class ContentStudioService : IContentStudioService
{
    private readonly AppDbContext _db;
    private readonly IAgentRunner _runner;

    public ContentStudioService(AppDbContext db, IAgentRunner runner)
    {
        _db = db;
        _runner = runner;
    }

    public async Task<List<ContentItemDto>> ListAsync(Guid? affiliateId, string? month, string? status, int limit)
    {
        limit = Math.Clamp(limit, 1, 300);
        var q = _db.ContentItems.AsNoTracking().AsQueryable();
        if (affiliateId is Guid a) q = q.Where(c => c.AffiliateId == a);
        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!ContentRules.IsMonth(month)) throw new ArgumentException("El mes debe tener la forma yyyy-MM.");
            q = q.Where(c => c.PlanMonth == month);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!ContentStatus.All.Contains(status)) throw new ArgumentException("Estado desconocido.");
            q = q.Where(c => c.Status == status);
        }

        var rows = await q.OrderBy(c => c.ScheduledFor).ThenBy(c => c.Network).Take(limit).ToListAsync();
        var ids = rows.Select(r => r.AffiliateId).Distinct().ToList();
        var names = await _db.Affiliates.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Name);
        return rows.Select(r => ToDto(r, names.GetValueOrDefault(r.AffiliateId) ?? "—")).ToList();
    }

    public async Task<ContentQuotaDto> GetQuotaAsync(Guid affiliateId, string month)
    {
        if (!ContentRules.IsMonth(month)) throw new ArgumentException("El mes debe tener la forma yyyy-MM.");
        await EnsureAffiliateAsync(affiliateId);
        var plan = await _db.AgentAffiliatePlans.AsNoTracking().FirstOrDefaultAsync(p => p.AffiliateId == affiliateId);
        var counts = await _db.ContentItems.AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId && c.PlanMonth == month && c.Status != ContentStatus.Rejected)
            .GroupBy(c => c.Status).Select(g => new { Status = g.Key, N = g.Count() }).ToListAsync();
        int Of(string s) => counts.FirstOrDefault(c => c.Status == s)?.N ?? 0;
        var used = counts.Sum(c => c.N);
        var posts = plan?.PostsPerMonth ?? 0;
        return new ContentQuotaDto(affiliateId, month, posts, plan is null ? new List<string>() : ContentRules.SplitNetworks(plan.Networks),
            used, Math.Max(0, posts - used), Of(ContentStatus.Draft) + Of(ContentStatus.ChangesRequested), Of(ContentStatus.Approved),
            Of(ContentStatus.Published));
    }

    public async Task<ContentItemDto> CreateAsync(CreateContentItemRequest request, string? by)
    {
        var affiliate = await _db.Affiliates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.AffiliateId)
            ?? throw new KeyNotFoundException("Ese negocio no existe.");
        var plan = await _db.AgentAffiliatePlans.AsNoTracking().FirstOrDefaultAsync(p => p.AffiliateId == request.AffiliateId);
        if (plan is null || plan.PostsPerMonth == 0 || string.IsNullOrWhiteSpace(plan.Networks))
            throw new ArgumentException("Este negocio no tiene un plan de publicaciones. Defínelo en Marca y marketing.");

        if (!ContentRules.TryParseDate(request.Date, out var date)) throw new ArgumentException("La fecha debe tener la forma yyyy-MM-dd.");
        var month = ContentRules.MonthOf(date);
        var network = (request.Network ?? string.Empty).Trim().ToLowerInvariant();
        var mediaKind = string.IsNullOrWhiteSpace(request.MediaKind) ? "none" : request.MediaKind.Trim().ToLowerInvariant();
        var title = (request.Title ?? string.Empty).Trim();
        var caption = (request.Caption ?? string.Empty).Trim();
        var hashtags = Blank(request.Hashtags);

        var error = ContentRules.Validate(network, title, caption, hashtags, mediaKind, ContentRules.SplitNetworks(plan.Networks).ToArray());
        if (error is not null) throw new ArgumentException(error);

        if (await ContentRules.CountUsedAsync(_db, request.AffiliateId, month) >= plan.PostsPerMonth)
            throw new ArgumentException($"El plan de {affiliate.Name} incluye {plan.PostsPerMonth} publicaciones al mes y {month} ya las tiene completas.");

        var item = new ContentItem
        {
            AffiliateId = request.AffiliateId, PlanMonth = month, ScheduledFor = date, Network = network,
            Occasion = Blank(request.Occasion), Title = title, Caption = caption, Hashtags = hashtags,
            MediaKind = mediaKind, MediaBrief = Blank(request.MediaBrief), MediaUrl = Blank(request.MediaUrl),
            Status = ContentStatus.Draft, Source = "manual", CreatedBy = by, UpdatedBy = by,
        };
        _db.ContentItems.Add(item);
        await _db.SaveChangesAsync();
        return ToDto(item, affiliate.Name);
    }

    public async Task<ContentItemDto> UpdateAsync(Guid id, UpdateContentItemRequest request, string? by)
    {
        var item = await _db.ContentItems.FirstOrDefaultAsync(c => c.Id == id) ?? throw new KeyNotFoundException("Esa pieza no existe.");
        if (item.Status is ContentStatus.Published or ContentStatus.Rejected)
            throw new ArgumentException("Una pieza publicada o rechazada ya no se edita.");

        var plan = await _db.AgentAffiliatePlans.AsNoTracking().FirstOrDefaultAsync(p => p.AffiliateId == item.AffiliateId);
        var planNetworks = plan is null ? Array.Empty<string>() : ContentRules.SplitNetworks(plan.Networks).ToArray();

        var network = request.Network is null ? item.Network : request.Network.Trim().ToLowerInvariant();
        var title = request.Title is null ? item.Title : request.Title.Trim();
        var caption = request.Caption is null ? item.Caption : request.Caption.Trim();
        var hashtags = request.Hashtags is null ? item.Hashtags : Blank(request.Hashtags);
        var mediaKind = request.MediaKind is null ? item.MediaKind : request.MediaKind.Trim().ToLowerInvariant();

        var error = ContentRules.Validate(network, title, caption, hashtags, mediaKind, planNetworks);
        if (error is not null) throw new ArgumentException(error);

        var date = item.ScheduledFor;
        var month = item.PlanMonth;
        if (request.Date is not null)
        {
            if (!ContentRules.TryParseDate(request.Date, out date)) throw new ArgumentException("La fecha debe tener la forma yyyy-MM-dd.");
            month = ContentRules.MonthOf(date);
            // Moverla a otro mes la cuenta en la cuota de ese mes.
            if (month != item.PlanMonth && plan is not null && await ContentRules.CountUsedAsync(_db, item.AffiliateId, month) >= plan.PostsPerMonth)
                throw new ArgumentException($"{month} ya tiene completas las publicaciones del plan.");
        }

        var textChanged = network != item.Network || title != item.Title || caption != item.Caption || hashtags != item.Hashtags
                          || date != item.ScheduledFor;

        item.Network = network;
        item.Title = title;
        item.Caption = caption;
        item.Hashtags = hashtags;
        item.MediaKind = mediaKind;
        item.ScheduledFor = date;
        item.PlanMonth = month;
        if (request.Occasion is not null) item.Occasion = Blank(request.Occasion);
        if (request.MediaBrief is not null) item.MediaBrief = Blank(request.MediaBrief);
        if (request.MediaUrl is not null) item.MediaUrl = Blank(request.MediaUrl);

        // Cambiar lo que se va a publicar invalida la aprobación y el veredicto anterior: hay que volver a revisarlo.
        if (textChanged)
        {
            item.Status = ContentStatus.Draft;
            item.ReviewVerdict = null;
            item.ReviewNotes = null;
            item.ReviewedAt = null;
            item.DecidedBy = null;
            item.DecidedAt = null;
        }
        else if (item.Status == ContentStatus.ChangesRequested)
        {
            item.Status = ContentStatus.Draft;
        }

        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = by;
        await _db.SaveChangesAsync();
        return ToDto(item, await NameOfAsync(item.AffiliateId));
    }

    public async Task<ContentItemDto> DecideAsync(Guid id, ContentDecisionRequest request, string? by)
    {
        var item = await _db.ContentItems.FirstOrDefaultAsync(c => c.Id == id) ?? throw new KeyNotFoundException("Esa pieza no existe.");
        if (item.Status is ContentStatus.Published or ContentStatus.Rejected)
            throw new ArgumentException("Esa pieza ya está cerrada.");

        var note = Blank(request.Note);
        switch ((request.Decision ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "approve":
                if (item.ReviewVerdict == "fail" && note is null)
                    throw new ArgumentException("El revisor marcó un fallo en esta pieza. Corrígela, o escribe una nota explicando por qué la apruebas igual.");
                item.Status = ContentStatus.Approved;
                break;
            case "reject":
                if (note is null) throw new ArgumentException("Escribe el motivo del rechazo.");
                item.Status = ContentStatus.Rejected;
                break;
            case "request_changes":
                if (note is null) throw new ArgumentException("Escribe qué hay que cambiar.");
                item.Status = ContentStatus.ChangesRequested;
                break;
            default:
                throw new ArgumentException("La decisión debe ser approve, reject o request_changes.");
        }

        item.StaffNote = note;
        item.DecidedBy = by;
        item.DecidedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = by;
        await _db.SaveChangesAsync();
        return ToDto(item, await NameOfAsync(item.AffiliateId));
    }

    public async Task<AgentJobTicketDto> RequestPlanAsync(RequestPlanRequest request, string? by)
    {
        var affiliate = await _db.Affiliates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.AffiliateId)
            ?? throw new KeyNotFoundException("Ese negocio no existe.");
        if (!ContentRules.IsMonth(request.Month)) throw new ArgumentException("El mes debe tener la forma yyyy-MM.");
        var quota = await GetQuotaAsync(request.AffiliateId, request.Month);
        if (quota.PostsPerMonth == 0 || quota.Networks.Count == 0)
            throw new ArgumentException("Este negocio no tiene un plan de publicaciones. Defínelo en Marca y marketing.");
        if (quota.Remaining == 0)
            throw new ArgumentException($"{request.Month} ya tiene completas las {quota.PostsPerMonth} publicaciones del plan.");

        var text = $"Planifica el contenido del mes {request.Month} para {affiliate.Name}. Faltan {quota.Remaining} piezas de {quota.PostsPerMonth}.";
        if (!string.IsNullOrWhiteSpace(request.Instructions))
            text += $"\n\nIndicaciones del equipo para este mes (síguelas):\n{request.Instructions.Trim()}";

        var jobId = await _runner.EnqueueAsync("content-planner", request.AffiliateId,
            new[] { new AgentChatMessage("user", text) }, by, null, CancellationToken.None);
        return new AgentJobTicketDto(jobId);
    }

    public async Task<AgentJobTicketDto> RequestReviewAsync(RequestReviewRequest request, string? by)
    {
        var affiliate = await _db.Affiliates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.AffiliateId)
            ?? throw new KeyNotFoundException("Ese negocio no existe.");
        if (!ContentRules.IsMonth(request.Month)) throw new ArgumentException("El mes debe tener la forma yyyy-MM.");
        var pending = await _db.ContentItems.CountAsync(c => c.AffiliateId == request.AffiliateId && c.PlanMonth == request.Month
            && (c.Status == ContentStatus.Draft || c.Status == ContentStatus.ChangesRequested) && c.ReviewVerdict == null);
        if (pending == 0) throw new ArgumentException("No hay borradores sin revisar en ese mes.");

        var jobId = await _runner.EnqueueAsync("content-reviewer", request.AffiliateId,
            new[] { new AgentChatMessage("user", $"Revisa los borradores de {request.Month} de {affiliate.Name} que todavía no tienen veredicto.") },
            by, null, CancellationToken.None);
        return new AgentJobTicketDto(jobId);
    }

    private async Task EnsureAffiliateAsync(Guid affiliateId)
    {
        if (!await _db.Affiliates.AnyAsync(a => a.Id == affiliateId)) throw new KeyNotFoundException("Ese negocio no existe.");
    }

    private async Task<string> NameOfAsync(Guid affiliateId) =>
        await _db.Affiliates.AsNoTracking().Where(a => a.Id == affiliateId).Select(a => a.Name).FirstOrDefaultAsync() ?? "—";

    private static string? Blank(string? value)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static ContentItemDto ToDto(ContentItem c, string affiliateName) => new(c.Id, c.AffiliateId, affiliateName, c.PlanMonth,
        c.ScheduledFor, c.Network, c.Occasion, c.Title, c.Caption, c.Hashtags, c.MediaKind, c.MediaBrief, c.MediaUrl, c.Status,
        c.Source, c.ReviewVerdict, c.ReviewNotes, c.ReviewedAt, c.StaffNote, c.DecidedBy, c.DecidedAt, c.CreatedAt);
}
