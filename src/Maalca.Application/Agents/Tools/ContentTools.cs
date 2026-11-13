using System.Text;
using System.Text.Json;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Agents.Tools;

// Herramientas del estudio de contenido. Las reglas (cuota, redes del plan, tamaños) viven aquí y en
// ContentRules, en código: el prompt las explica, pero no depende de él para cumplirlas.
// Todas son internas (InternalOnly) y trabajan sobre el negocio del contexto, nunca uno que elija el modelo.

internal static class ContentToolInput
{
    public static string? Str(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()?.Trim()
            : null;
}

public class ContentPlanStatusTool : IAgentTool
{
    private readonly AppDbContext _db;
    public ContentPlanStatusTool(AppDbContext db) => _db = db;

    public string Name => "content_plan_status";
    public string Description =>
        "Estado del plan de publicaciones de un mes para el negocio actual: cuántas incluye el plan, en qué redes, cuántas ya " +
        "existen y cuáles son (fecha, red, ocasión, título). Llámala antes de planear para no pasarte del cupo ni repetir.";
    public string InputSchemaJson =>
        "{\"type\":\"object\",\"properties\":{\"month\":{\"type\":\"string\",\"description\":\"Mes con la forma yyyy-MM.\"}},\"required\":[\"month\"],\"additionalProperties\":false}";
    public bool InternalOnly => true;

    public async Task<string> ExecuteAsync(AgentToolContext context, JsonElement input, CancellationToken ct)
    {
        if (context.AffiliateId is not Guid affiliateId) return "Esta herramienta necesita un negocio y esta conversación no tiene uno.";
        var month = ContentToolInput.Str(input, "month");
        if (!ContentRules.IsMonth(month)) return "El mes debe tener la forma yyyy-MM.";

        var plan = await _db.AgentAffiliatePlans.AsNoTracking().FirstOrDefaultAsync(p => p.AffiliateId == affiliateId, ct);
        if (plan is null || plan.PostsPerMonth == 0) return "Este negocio no tiene un plan de publicaciones definido. No se puede planear.";

        var raw = await _db.ContentItems.AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId && c.PlanMonth == month && c.Status != ContentStatus.Rejected)
            .OrderBy(c => c.ScheduledFor)
            .Select(c => new { c.ScheduledFor, c.Network, c.Occasion, c.Title, c.Status })
            .ToListAsync(ct);
        var items = raw.Select(c => new { Date = c.ScheduledFor.ToString("yyyy-MM-dd"), c.Network, c.Occasion, c.Title, c.Status }).ToList();

        return JsonSerializer.Serialize(new
        {
            month,
            postsPerMonth = plan.PostsPerMonth,
            networks = ContentRules.SplitNetworks(plan.Networks),
            alreadyPlanned = items.Count,
            remaining = Math.Max(0, plan.PostsPerMonth - items.Count),
            existing = items,
        }, AgentToolJson.Options);
    }
}

public class ContentSaveDraftsTool : IAgentTool
{
    private readonly AppDbContext _db;
    public ContentSaveDraftsTool(AppDbContext db) => _db = db;

    public string Name => "content_save_drafts";
    public string Description =>
        "Guarda varias piezas como borradores del negocio actual, en una sola llamada. Cada pieza se valida: la red debe estar en el " +
        "plan, la fecha dentro del mes y no se puede pasar del cupo de publicaciones. Las que no cumplan vuelven con el motivo; " +
        "las demás se guardan. Los borradores esperan revisión humana.";
    public string InputSchemaJson =>
        "{\"type\":\"object\",\"properties\":{" +
        "\"month\":{\"type\":\"string\",\"description\":\"Mes con la forma yyyy-MM.\"}," +
        "\"items\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{" +
        "\"network\":{\"type\":\"string\",\"enum\":[\"facebook\",\"instagram\",\"tiktok\",\"youtube\"]}," +
        "\"date\":{\"type\":\"string\",\"description\":\"Fecha yyyy-MM-dd dentro del mes.\"}," +
        "\"occasion\":{\"type\":\"string\",\"description\":\"Fecha o temporada que motiva la pieza (opcional).\"}," +
        "\"title\":{\"type\":\"string\",\"description\":\"Título corto interno, máximo 160 caracteres.\"}," +
        "\"caption\":{\"type\":\"string\",\"description\":\"Texto listo para publicar, máximo 2200 caracteres.\"}," +
        "\"hashtags\":{\"type\":\"string\"}," +
        "\"mediaKind\":{\"type\":\"string\",\"enum\":[\"none\",\"image\",\"video\"]}," +
        "\"mediaBrief\":{\"type\":\"string\",\"description\":\"Qué imagen o video hace falta, con la toma concreta.\"}" +
        "},\"required\":[\"network\",\"date\",\"title\",\"caption\",\"mediaKind\"]}}" +
        "},\"required\":[\"month\",\"items\"],\"additionalProperties\":false}";
    public bool InternalOnly => true;

    public async Task<string> ExecuteAsync(AgentToolContext context, JsonElement input, CancellationToken ct)
    {
        if (context.AffiliateId is not Guid affiliateId) return "Esta herramienta necesita un negocio y esta conversación no tiene uno.";
        var month = ContentToolInput.Str(input, "month");
        if (!ContentRules.IsMonth(month)) return "El mes debe tener la forma yyyy-MM.";
        if (input.ValueKind != JsonValueKind.Object || !input.TryGetProperty("items", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return "Falta la lista items.";

        var plan = await _db.AgentAffiliatePlans.AsNoTracking().FirstOrDefaultAsync(p => p.AffiliateId == affiliateId, ct);
        if (plan is null || plan.PostsPerMonth == 0) return "Este negocio no tiene un plan de publicaciones definido. No se puede guardar nada.";
        var networks = ContentRules.SplitNetworks(plan.Networks).ToArray();
        var used = await ContentRules.CountUsedAsync(_db, affiliateId, month!);

        var sb = new StringBuilder();
        var saved = 0;
        var index = 0;
        foreach (var el in arr.EnumerateArray())
        {
            index++;
            var network = ContentToolInput.Str(el, "network")?.ToLowerInvariant() ?? string.Empty;
            var title = ContentToolInput.Str(el, "title") ?? string.Empty;
            var caption = ContentToolInput.Str(el, "caption") ?? string.Empty;
            var hashtags = ContentToolInput.Str(el, "hashtags");
            var mediaKind = ContentToolInput.Str(el, "mediaKind")?.ToLowerInvariant() ?? "none";

            string? error = null;
            if (used >= plan.PostsPerMonth) error = $"Se alcanzó el cupo del mes ({plan.PostsPerMonth}).";
            else if (!ContentRules.TryParseDate(ContentToolInput.Str(el, "date"), out var date)) error = "La fecha debe ser yyyy-MM-dd.";
            else if (ContentRules.MonthOf(date) != month) error = $"La fecha debe caer dentro de {month}.";
            else
            {
                error = ContentRules.Validate(network, title, caption, hashtags, mediaKind, networks);
                if (error is null)
                {
                    _db.ContentItems.Add(new ContentItem
                    {
                        AffiliateId = affiliateId, PlanMonth = month!, ScheduledFor = date, Network = network,
                        Occasion = Truncate(ContentToolInput.Str(el, "occasion"), 120), Title = title, Caption = caption, Hashtags = hashtags,
                        MediaKind = mediaKind, MediaBrief = ContentToolInput.Str(el, "mediaBrief"),
                        Status = ContentStatus.Draft, Source = "agent", CreatedBy = "agent", UpdatedBy = "agent",
                    });
                    used++;
                    saved++;
                }
            }
            if (error is not null) sb.AppendLine($"Pieza {index}: NO guardada. {error}");
        }

        if (saved > 0) await _db.SaveChangesAsync(ct);
        sb.Insert(0, $"Guardadas {saved} de {index} piezas como borrador. Cupo usado: {used} de {plan.PostsPerMonth}.\n");
        return sb.ToString().TrimEnd();
    }

    private static string? Truncate(string? v, int max) => v is null ? null : v.Length <= max ? v : v[..max];
}

public class ContentListDraftsTool : IAgentTool
{
    private readonly AppDbContext _db;
    public ContentListDraftsTool(AppDbContext db) => _db = db;

    public string Name => "content_list_drafts";
    public string Description =>
        "Lista los borradores del negocio actual en un mes que esperan revisión (sin veredicto todavía), con su id, red, fecha, " +
        "ocasión, título, texto, hashtags y qué medio llevan.";
    public string InputSchemaJson =>
        "{\"type\":\"object\",\"properties\":{\"month\":{\"type\":\"string\",\"description\":\"Mes con la forma yyyy-MM.\"}},\"required\":[\"month\"],\"additionalProperties\":false}";
    public bool InternalOnly => true;

    public async Task<string> ExecuteAsync(AgentToolContext context, JsonElement input, CancellationToken ct)
    {
        if (context.AffiliateId is not Guid affiliateId) return "Esta herramienta necesita un negocio y esta conversación no tiene uno.";
        var month = ContentToolInput.Str(input, "month");
        if (!ContentRules.IsMonth(month)) return "El mes debe tener la forma yyyy-MM.";

        var raw = await _db.ContentItems.AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId && c.PlanMonth == month && c.ReviewVerdict == null
                        && (c.Status == ContentStatus.Draft || c.Status == ContentStatus.ChangesRequested))
            .OrderBy(c => c.ScheduledFor)
            .Select(c => new { c.Id, c.ScheduledFor, c.Network, c.Occasion, c.Title, c.Caption, c.Hashtags, c.MediaKind, c.MediaBrief })
            .ToListAsync(ct);
        var rows = raw.Select(c => new { c.Id, Date = c.ScheduledFor.ToString("yyyy-MM-dd"), c.Network, c.Occasion, c.Title, c.Caption, c.Hashtags, c.MediaKind, c.MediaBrief }).ToList();

        return rows.Count == 0 ? "No hay borradores sin revisar en ese mes." : JsonSerializer.Serialize(rows, AgentToolJson.Options);
    }
}

public class ContentRecordReviewsTool : IAgentTool
{
    private readonly AppDbContext _db;
    public ContentRecordReviewsTool(AppDbContext db) => _db = db;

    public string Name => "content_record_reviews";
    public string Description =>
        "Registra el veredicto de revisión de varias piezas del negocio actual en una sola llamada. Veredicto: pass, warn o fail, " +
        "con notas concretas. No aprueba nada: solo una persona puede aprobar.";
    public string InputSchemaJson =>
        "{\"type\":\"object\",\"properties\":{\"reviews\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{" +
        "\"id\":{\"type\":\"string\",\"description\":\"Id de la pieza, tal como lo dio content_list_drafts.\"}," +
        "\"verdict\":{\"type\":\"string\",\"enum\":[\"pass\",\"warn\",\"fail\"]}," +
        "\"notes\":{\"type\":\"string\",\"description\":\"Qué corregir y cómo; si pasa, por qué.\"}" +
        "},\"required\":[\"id\",\"verdict\",\"notes\"]}}},\"required\":[\"reviews\"],\"additionalProperties\":false}";
    public bool InternalOnly => true;

    private static readonly string[] Verdicts = { "pass", "warn", "fail" };

    public async Task<string> ExecuteAsync(AgentToolContext context, JsonElement input, CancellationToken ct)
    {
        if (context.AffiliateId is not Guid affiliateId) return "Esta herramienta necesita un negocio y esta conversación no tiene uno.";
        if (input.ValueKind != JsonValueKind.Object || !input.TryGetProperty("reviews", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return "Falta la lista reviews.";

        var sb = new StringBuilder();
        var saved = 0;
        var total = 0;
        foreach (var el in arr.EnumerateArray())
        {
            total++;
            var verdict = ContentToolInput.Str(el, "verdict")?.ToLowerInvariant() ?? string.Empty;
            var notes = ContentToolInput.Str(el, "notes");
            if (!Guid.TryParse(ContentToolInput.Str(el, "id"), out var id)) { sb.AppendLine($"Revisión {total}: id inválido."); continue; }
            if (!Verdicts.Contains(verdict)) { sb.AppendLine($"Revisión {total}: el veredicto debe ser pass, warn o fail."); continue; }
            if (string.IsNullOrWhiteSpace(notes)) { sb.AppendLine($"Revisión {total}: faltan las notas."); continue; }

            // Solo piezas de este negocio y aún abiertas: el modelo no puede tocar otras.
            var item = await _db.ContentItems.FirstOrDefaultAsync(c => c.Id == id && c.AffiliateId == affiliateId
                && (c.Status == ContentStatus.Draft || c.Status == ContentStatus.ChangesRequested), ct);
            if (item is null) { sb.AppendLine($"Revisión {total}: esa pieza no existe o ya no está abierta."); continue; }

            item.ReviewVerdict = verdict;
            item.ReviewNotes = notes;
            item.ReviewedAt = DateTime.UtcNow;
            saved++;
        }

        if (saved > 0) await _db.SaveChangesAsync(ct);
        sb.Insert(0, $"Registradas {saved} de {total} revisiones.\n");
        return sb.ToString().TrimEnd();
    }
}
