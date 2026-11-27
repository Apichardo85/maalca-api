using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Maalca.Application.Agents;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Application.Services;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Creator;

/// <summary>
/// Traducción literaria de obras (WorkTranslation.cs).
///
/// - El modelo sale del registro de agentes (AgentModels, nivel "premium") y la llave del proveedor
///   (/ops o ANTHROPIC_API_KEY): cambiar de modelo es editar esa fila, no este código.
/// - Cada capítulo se traduce en dos pasadas (traducir; revisar contra el original) con el capítulo
///   anterior como contexto, para que la voz y los términos no cambien de un capítulo a otro.
/// - El resultado queda en borrador. Publicar es decisión del autor, capítulo por capítulo.
/// </summary>
public class WorkTranslationService : IWorkTranslationService
{
    /// <summary>Idiomas destino permitidos (código → nombre para el modelo).</summary>
    public static readonly IReadOnlyDictionary<string, string> Languages = new Dictionary<string, string>
    {
        ["en"] = "English",
    };

    private const string ModelTier = "premium";
    private const int MaxChaptersPerRequest = 500;
    private const int MaxAttempts = 3;
    private const int MaxChapterChars = 400_000;
    private const int ContextChars = 6_000;
    private static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(2);

    private static readonly Regex TitleTag = new(@"<title>([\s\S]*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BodyTag = new(@"<body>([\s\S]*)</body>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly IEnumerable<IAgentModelClient> _clients;
    private readonly IConfiguration _config;
    private readonly ILogger<WorkTranslationService> _logger;
    private readonly string _secret;

    public WorkTranslationService(AppDbContext db, IEnumerable<IAgentModelClient> clients, IConfiguration config, ILogger<WorkTranslationService> logger)
    {
        _db = db;
        _clients = clients;
        _config = config;
        _logger = logger;
        // Misma llave de cifrado que AgentRunner para leer la llave del proveedor guardada en /ops.
        _secret = config["Jwt:Key"] ?? "MaalcaSecretKey12345678901234567890";
    }

    // ── Panel del autor ─────────────────────────────────────────────────────────────

    public async Task<WorkTranslationOverviewDto?> GetOverviewAsync(Guid affiliateId, Guid workId, string language)
    {
        var lang = Lang(language);
        var work = await _db.Works.AsNoTracking().FirstOrDefaultAsync(w => w.Id == workId && w.AffiliateId == affiliateId);
        if (work is null) return null;

        var settings = await _db.WorkTranslationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.WorkId == workId && s.Language == lang);
        var chapters = await _db.WorkChapters.AsNoTracking()
            .Where(c => c.WorkId == workId)
            .OrderBy(c => c.Number)
            .Select(c => new { c.Id, c.Number, c.Title, c.ContentHtml, c.WordCount })
            .ToListAsync();
        var translations = await _db.WorkChapterTranslations.AsNoTracking()
            .Where(t => t.WorkId == workId && t.Language == lang)
            .ToDictionaryAsync(t => t.ChapterId);

        var rows = chapters.Select(c =>
        {
            translations.TryGetValue(c.Id, out var t);
            return new WorkChapterTranslationRefDto(
                c.Id, c.Number, c.Title, c.WordCount,
                t?.Status, t?.Title, t?.WordCount ?? 0,
                t is not null && IsStale(t, Hash(c.Title, c.ContentHtml)),
                t?.ReviewedAt is not null, t?.Error, t?.CostUsd ?? 0m, t?.UpdatedAt ?? t?.CreatedAt);
        }).ToList();

        var engine = await ResolveEngineAsync(CancellationToken.None);
        return new WorkTranslationOverviewDto(
            work.Id, work.Title, ToSettingsDto(settings, lang), rows,
            engine?.ModelId, engine is not null,
            translations.Values.Sum(t => t.CostUsd));
    }

    public async Task<WorkTranslationSettingsDto?> SaveSettingsAsync(Guid affiliateId, Guid workId, string language, UpsertWorkTranslationSettingsRequest r)
    {
        var lang = Lang(language);
        if (!await OwnsWorkAsync(affiliateId, workId)) return null;
        if (r.Credit is { Length: > 200 }) throw new ArgumentException("El crédito no puede pasar de 200 caracteres.");

        var s = await _db.WorkTranslationSettings.FirstOrDefaultAsync(x => x.WorkId == workId && x.Language == lang);
        if (s is null)
        {
            s = new WorkTranslationSettings { WorkId = workId, Language = lang, CreatedAt = DateTime.UtcNow };
            _db.WorkTranslationSettings.Add(s);
        }
        else
        {
            s.UpdatedAt = DateTime.UtcNow;
        }
        s.Brief = Clean(r.Brief);
        s.Glossary = Clean(r.Glossary);
        s.Credit = Clean(r.Credit);
        await _db.SaveChangesAsync();
        return ToSettingsDto(s, lang);
    }

    public async Task<QueueWorkTranslationResultDto?> QueueAsync(Guid affiliateId, Guid workId, string language, QueueWorkTranslationRequest r)
    {
        var lang = Lang(language);
        if (!await OwnsWorkAsync(affiliateId, workId)) return null;
        if (r.ChapterIds is { Count: > MaxChaptersPerRequest })
            throw new ArgumentException($"Máximo {MaxChaptersPerRequest} capítulos por pedido.");

        var explicitIds = r.ChapterIds?.Distinct().ToHashSet();
        var chapters = await _db.WorkChapters.AsNoTracking()
            .Where(c => c.WorkId == workId && (explicitIds == null || explicitIds.Contains(c.Id)))
            .Select(c => new { c.Id, c.Title, c.ContentHtml })
            .ToListAsync();
        var existing = await _db.WorkChapterTranslations
            .Where(t => t.WorkId == workId && t.Language == lang)
            .ToDictionaryAsync(t => t.ChapterId);

        var now = DateTime.UtcNow;
        int queued = 0, skipped = 0;
        foreach (var c in chapters)
        {
            existing.TryGetValue(c.Id, out var t);
            if (string.IsNullOrWhiteSpace(c.ContentHtml) || t?.Status is "queued" or "generating") { skipped++; continue; }

            if (!r.Force && t is not null)
            {
                // Lo revisado o publicado por el autor no se pisa sin confirmación.
                if (t.Status == "published" || t.ReviewedAt is not null) { skipped++; continue; }
                // En "lo que falta", un borrador al día no se rehace; pedido por capítulo, sí.
                if (explicitIds is null && t.Status == "draft" && !IsStale(t, Hash(c.Title, c.ContentHtml))) { skipped++; continue; }
            }

            if (t is null)
            {
                t = new WorkChapterTranslation { WorkId = workId, ChapterId = c.Id, Language = lang, CreatedAt = now };
                _db.WorkChapterTranslations.Add(t);
            }
            t.Status = "queued";
            t.Error = null;
            t.Attempts = 0;
            t.UpdatedAt = now;
            queued++;
        }
        await _db.SaveChangesAsync();
        return new QueueWorkTranslationResultDto(queued, skipped);
    }

    public async Task<WorkChapterTranslationDetailDto?> GetChapterAsync(Guid affiliateId, Guid workId, string language, Guid chapterId)
    {
        var lang = Lang(language);
        var ch = await OwnedChapterAsync(affiliateId, workId, chapterId);
        if (ch is null) return null;
        var t = await _db.WorkChapterTranslations.AsNoTracking().FirstOrDefaultAsync(x => x.ChapterId == chapterId && x.Language == lang);
        return ToDetail(ch, t);
    }

    public async Task<WorkChapterTranslationDetailDto?> UpdateChapterAsync(Guid affiliateId, Guid workId, string language, Guid chapterId, UpdateWorkChapterTranslationRequest r)
    {
        var lang = Lang(language);
        var ch = await OwnedChapterAsync(affiliateId, workId, chapterId);
        if (ch is null) return null;
        if (string.IsNullOrWhiteSpace(r.Title)) throw new ArgumentException("El título traducido es obligatorio.");
        if (r.Title.Trim().Length > 200) throw new ArgumentException("El título no puede pasar de 200 caracteres.");
        if (r.ContentHtml is { Length: > MaxChapterChars }) throw new ArgumentException("El capítulo es demasiado largo.");

        var now = DateTime.UtcNow;
        var t = await _db.WorkChapterTranslations.FirstOrDefaultAsync(x => x.ChapterId == chapterId && x.Language == lang);
        if (t is null)
        {
            // Traducir a mano, sin pedir borrador al modelo, también vale.
            t = new WorkChapterTranslation { WorkId = workId, ChapterId = chapterId, Language = lang, CreatedAt = now };
            _db.WorkChapterTranslations.Add(t);
        }
        t.Title = r.Title.Trim();
        t.ContentHtml = HtmlScrub.SanitizeRichText(r.ContentHtml);
        t.WordCount = WorkService.CountWords(t.ContentHtml);
        // Guardar a mano detiene un borrador en cola: lo del autor manda.
        t.Status = t.Status == "published" ? "published" : "draft";
        t.Error = null;
        t.ReviewedAt = now;
        t.SourceHash = Hash(ch.Title, ch.ContentHtml);
        t.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return ToDetail(ch, t);
    }

    public async Task<WorkChapterTranslationDetailDto?> SetPublishedAsync(Guid affiliateId, Guid workId, string language, Guid chapterId, bool published)
    {
        var lang = Lang(language);
        var ch = await OwnedChapterAsync(affiliateId, workId, chapterId);
        if (ch is null) return null;
        var t = await _db.WorkChapterTranslations.FirstOrDefaultAsync(x => x.ChapterId == chapterId && x.Language == lang);
        if (t is null || string.IsNullOrWhiteSpace(t.ContentHtml) || string.IsNullOrWhiteSpace(t.Title))
            throw new ArgumentException("No hay traducción que publicar en este capítulo.");
        if (t.Status is "queued" or "generating")
            throw new ArgumentException("Espera a que termine el borrador, o guárdalo a mano, antes de publicar.");

        var now = DateTime.UtcNow;
        t.Status = published ? "published" : "draft";
        t.PublishedAt = published ? now : null;
        t.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return ToDetail(ch, t);
    }

    // ── Worker ──────────────────────────────────────────────────────────────────────

    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Un reinicio del API a mitad de un capítulo lo deja en "generating": vuelve a la cola (o falla tras 3 intentos).
        var stuckBefore = now - StuckAfter;
        await _db.WorkChapterTranslations
            .Where(t => t.Status == "generating" && t.UpdatedAt < stuckBefore && t.Attempts >= MaxAttempts)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "failed").SetProperty(t => t.Error, "Se interrumpió varias veces. Pide el borrador de nuevo.").SetProperty(t => t.UpdatedAt, now), ct);
        await _db.WorkChapterTranslations
            .Where(t => t.Status == "generating" && t.UpdatedAt < stuckBefore)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "queued").SetProperty(t => t.UpdatedAt, now), ct);

        // Sin modelo o sin llave la cola espera; no se queman intentos.
        var engine = await ResolveEngineAsync(ct);
        if (engine is null) return false;

        var retryBefore = now - RetryAfter;
        var nextId = await _db.WorkChapterTranslations.AsNoTracking()
            .Where(t => t.Status == "queued" && (t.Attempts == 0 || t.UpdatedAt < retryBefore))
            .OrderBy(t => t.UpdatedAt)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);
        if (nextId is null) return false;

        // Reclamar: solo una instancia del worker se queda con la fila.
        var claimed = await _db.WorkChapterTranslations
            .Where(t => t.Id == nextId && t.Status == "queued")
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, "generating")
                .SetProperty(t => t.Attempts, t => t.Attempts + 1)
                .SetProperty(t => t.UpdatedAt, now), ct);
        if (claimed == 0) return true;

        var row = await _db.WorkChapterTranslations.AsNoTracking().FirstAsync(t => t.Id == nextId, ct);
        var outcome = await TranslateAsync(row, engine, ct);

        // El autor pudo guardar a mano mientras tanto: si ya no está "generating", su versión manda.
        var current = await _db.WorkChapterTranslations.FirstOrDefaultAsync(x => x.Id == nextId, ct);
        if (current is null || current.Status != "generating") return true;

        current.UpdatedAt = DateTime.UtcNow;
        current.TokensInput += outcome.TokensIn;
        current.TokensOutput += outcome.TokensOut;
        current.CostUsd += outcome.Cost;
        current.ModelId = engine.ModelId;
        if (outcome.Title is not null && outcome.Html is not null)
        {
            current.Title = outcome.Title.Length > 200 ? outcome.Title[..200] : outcome.Title;
            current.ContentHtml = outcome.Html;
            current.WordCount = WorkService.CountWords(outcome.Html);
            current.SourceHash = outcome.SourceHash;
            current.Status = "draft";
            current.Error = null;
            current.GeneratedAt = current.UpdatedAt;
            current.ReviewedAt = null;
            current.PublishedAt = null;
        }
        else if (outcome.Retry && current.Attempts < MaxAttempts)
        {
            current.Status = "queued";
            current.Error = outcome.Error;
        }
        else
        {
            current.Status = "failed";
            current.Error = outcome.Error ?? "No se pudo traducir este capítulo.";
        }
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("[Translation] {Chapter} {Lang} → {Status} ({In}/{Out} tok, ${Cost})",
            current.ChapterId, current.Language, current.Status, outcome.TokensIn, outcome.TokensOut, outcome.Cost);
        return true;
    }

    private sealed record Engine(IAgentModelClient Client, string BaseUrl, string ApiKey, string ModelId, decimal InPrice, decimal OutPrice);

    private sealed record Outcome(string? Title, string? Html, string? SourceHash, string? Error, bool Retry, int TokensIn, int TokensOut, decimal Cost);

    private async Task<Engine?> ResolveEngineAsync(CancellationToken ct)
    {
        var model = await _db.AgentModels.AsNoTracking().FirstOrDefaultAsync(m => m.Tier == ModelTier && m.IsActive, ct);
        if (model is null) return null;
        var provider = await _db.AgentProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Key == model.ProviderKey && p.IsActive, ct);
        if (provider is null) return null;
        var client = _clients.FirstOrDefault(c => c.Kind == provider.Kind);
        if (client is null) return null;

        var key = SecretBox.Unprotect(provider.ApiKeyEnc, _secret);
        if (string.IsNullOrWhiteSpace(key) && provider.Kind == "anthropic")
            key = _config["Anthropic:ApiKey"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) return null;

        return new Engine(client, provider.BaseUrl, key, model.ModelId, model.InputPricePerMTok, model.OutputPricePerMTok);
    }

    private async Task<Outcome> TranslateAsync(WorkChapterTranslation row, Engine engine, CancellationToken ct)
    {
        var work = await _db.Works.AsNoTracking().FirstAsync(w => w.Id == row.WorkId, ct);
        var ch = await _db.WorkChapters.AsNoTracking().FirstAsync(c => c.Id == row.ChapterId, ct);
        var settings = await _db.WorkTranslationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.WorkId == row.WorkId && s.Language == row.Language, ct);
        var total = await _db.WorkChapters.CountAsync(c => c.WorkId == row.WorkId, ct);

        // Continuidad: el capítulo anterior y, si existe, cómo quedó en el otro idioma.
        var prev = await _db.WorkChapters.AsNoTracking()
            .Where(c => c.WorkId == row.WorkId && c.Number < ch.Number)
            .OrderByDescending(c => c.Number)
            .FirstOrDefaultAsync(ct);
        WorkChapterTranslation? prevT = null;
        if (prev is not null)
        {
            prevT = await _db.WorkChapterTranslations.AsNoTracking()
                .FirstOrDefaultAsync(t => t.ChapterId == prev.Id && t.Language == row.Language && t.ContentHtml != null, ct);
        }

        var sourceHash = Hash(ch.Title, ch.ContentHtml);
        var langName = Languages[row.Language];
        var system = SystemPrompt(work, settings, langName);
        var words = Math.Max(ch.WordCount, WorkService.CountWords(ch.ContentHtml));
        var maxTokens = Math.Clamp(words * 4 + 1000, 2000, 16000);

        var context = new StringBuilder();
        if (prev is not null)
        {
            context.AppendLine("Contexto de continuidad (NO lo traduzcas; úsalo para mantener voz y términos):");
            context.AppendLine($"<previous_source title=\"{Attr(prev.Title)}\">{Tail(prev.ContentHtml, ContextChars)}</previous_source>");
            if (prevT is not null)
                context.AppendLine($"<previous_translation title=\"{Attr(prevT.Title)}\">{Tail(prevT.ContentHtml, ContextChars)}</previous_translation>");
            context.AppendLine();
        }

        var source = $"<source_title>{ch.Title}</source_title>\n<source_html>\n{ch.ContentHtml}\n</source_html>";
        var pass1 = $"{context}Capítulo {ch.Number} de {total}.\n{source}\n\nTraduce al {langName}. Responde SOLO con:\n<title>título traducido</title>\n<body>HTML traducido</body>";

        int tin = 0, tout = 0;
        try
        {
            var first = await CallAsync(engine, system, pass1, maxTokens, ct);
            tin += first.TokensInput; tout += first.TokensOutput;
            var stop = CheckStop(first);
            if (stop is not null) return new Outcome(null, null, null, stop, false, tin, tout, Cost(engine, tin, tout));
            var draft = Parse(first.Text);
            if (draft is null)
                return new Outcome(null, null, null, "El modelo respondió en un formato inesperado. Pide el borrador de nuevo.", true, tin, tout, Cost(engine, tin, tout));

            // Segunda pasada: releer contra el original y corregir sentido, omisiones y tono.
            var pass2 = $"{source}\n\n<draft_title>{draft.Value.Title}</draft_title>\n<draft_html>\n{draft.Value.Html}\n</draft_html>\n\n" +
                        "Revisa este borrador de traducción contra el original, frase por frase: errores de sentido, omisiones, añadidos, " +
                        "falsos amigos, calcos, cambios de registro o de tono, y la estructura HTML (mismos <p> y <br>). " +
                        "Corrige lo que haga falta. Si está bien, devuélvelo igual. Responde SOLO con:\n<title>…</title>\n<body>…</body>";
            var second = await CallAsync(engine, system, pass2, maxTokens, ct);
            tin += second.TokensInput; tout += second.TokensOutput;
            var final = CheckStop(second) is null ? Parse(second.Text) : null;
            var chosen = final ?? draft.Value;

            var html = HtmlScrub.SanitizeRichText(chosen.Html);
            if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(chosen.Title))
                return new Outcome(null, null, null, "El modelo devolvió una traducción vacía. Pide el borrador de nuevo.", true, tin, tout, Cost(engine, tin, tout));
            return new Outcome(chosen.Title.Trim(), html, sourceHash, null, false, tin, tout, Cost(engine, tin, tout));
        }
        catch (AgentModelException ex)
        {
            // 429/5xx/red: se reintenta más tarde. 4xx: es definitivo (llave, modelo, petición).
            var retry = ex.StatusCode is null or 429 or >= 500;
            _logger.LogWarning("[Translation] {Chapter}: {Message}", row.ChapterId, ex.Message);
            return new Outcome(null, null, null, ex.Message, retry, tin, tout, Cost(engine, tin, tout));
        }
    }

    private static Task<ModelResponse> CallAsync(Engine e, string system, string user, int maxTokens, CancellationToken ct)
        => e.Client.CompleteAsync(new ModelRequest(
            e.BaseUrl, e.ApiKey, e.ModelId, system,
            new[] { new ModelMessage { Role = "user", Text = user } },
            Array.Empty<ModelToolSpec>(), maxTokens,
            CacheSystemPrompt: true, LongRunning: true), ct);

    private static string? CheckStop(ModelResponse r) => r.StopReason switch
    {
        "refusal" => "El modelo no tradujo este capítulo por su contenido. Tradúcelo a mano aquí mismo y guárdalo.",
        "max_tokens" => "El capítulo es demasiado largo para una sola pasada. Divídelo en dos capítulos y pide el borrador de nuevo.",
        _ => null,
    };

    private static (string Title, string Html)? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var title = TitleTag.Match(text);
        var body = BodyTag.Match(text);
        if (!title.Success || !body.Success) return null;
        return (title.Groups[1].Value.Trim(), body.Groups[1].Value.Trim());
    }

    private static string SystemPrompt(Work work, WorkTranslationSettings? s, string langName)
    {
        var poetry = work.Kind == "poetry";
        var sb = new StringBuilder();
        sb.AppendLine($"Eres un traductor literario profesional. Traduces del español (con giros del Caribe, en especial dominicanos) al {langName} la obra «{work.Title}», de su autor, para publicarla en su espacio de lectura. El autor revisa y firma cada traducción: entrega un borrador con calidad de publicación.");
        sb.AppendLine();
        sb.AppendLine("Principios:");
        sb.AppendLine("- Fidelidad a la voz: conserva el registro, el ritmo, la crudeza o la ternura del original. No suavices, no censures, no moralices, no expliques.");
        sb.AppendLine("- Traduce sentido e intención, no palabra por palabra. Evita calcos y falsos amigos.");
        sb.AppendLine("- No agregues ni quites contenido. Sin notas, comentarios, aclaraciones ni títulos que no existan en el original.");
        sb.AppendLine("- Nombres propios, marcas y lugares quedan como en el original. Lo que ya está en el idioma destino se deja igual.");
        sb.AppendLine("- Estructura HTML idéntica: los mismos <p>, <br>, <em> y <strong>, en el mismo orden. Solo cambia el texto.");
        if (poetry)
            sb.AppendLine("- Es poesía: traduce verso por verso; cada verso sigue en su propia línea separada por <br>. Cuida la música y la imagen por encima de la rima; no fuerces una rima que traicione el sentido.");
        sb.AppendLine();
        sb.AppendLine("Guía del autor para esta obra:");
        sb.AppendLine(string.IsNullOrWhiteSpace(s?.Brief) ? "(sin guía)" : s!.Brief);
        sb.AppendLine();
        sb.AppendLine("Glosario fijo (respétalo siempre):");
        sb.AppendLine(string.IsNullOrWhiteSpace(s?.Glossary) ? "(vacío)" : s!.Glossary);
        return sb.ToString();
    }

    // ── Ayudas ──────────────────────────────────────────────────────────────────────

    private static string Lang(string language)
    {
        var l = (language ?? string.Empty).Trim().ToLowerInvariant();
        if (!Languages.ContainsKey(l)) throw new ArgumentException($"Idioma no disponible: {language}.");
        return l;
    }

    private Task<bool> OwnsWorkAsync(Guid affiliateId, Guid workId)
        => _db.Works.AnyAsync(w => w.Id == workId && w.AffiliateId == affiliateId);

    private Task<WorkChapter?> OwnedChapterAsync(Guid affiliateId, Guid workId, Guid chapterId)
        => _db.WorkChapters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chapterId && c.WorkId == workId && c.Work!.AffiliateId == affiliateId);

    /// <summary>Huella del original (título + texto) para saber si la traducción quedó atrás.</summary>
    public static string Hash(string? title, string? html)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes((title ?? string.Empty) + "\n" + (html ?? string.Empty)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool IsStale(WorkChapterTranslation t, string currentHash)
        => t.Status is "draft" or "published" && t.SourceHash is not null && t.SourceHash != currentHash;

    private static decimal Cost(Engine e, int tin, int tout)
        => Math.Round(tin / 1_000_000m * e.InPrice + tout / 1_000_000m * e.OutPrice, 4);

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static string Attr(string? v) => (v ?? string.Empty).Replace("\"", "'");

    private static string Tail(string? html, int max)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        return html.Length <= max ? html : "…" + html[^max..];
    }

    private static WorkTranslationSettingsDto ToSettingsDto(WorkTranslationSettings? s, string lang)
        => new(lang, s?.Brief, s?.Glossary, s?.Credit);

    private static WorkChapterTranslationDetailDto ToDetail(WorkChapter ch, WorkChapterTranslation? t)
        => new(ch.Id, ch.Number, ch.Title, ch.ContentHtml,
            t?.Status, t?.Title, t?.ContentHtml,
            t is not null && IsStale(t, Hash(ch.Title, ch.ContentHtml)),
            t?.ReviewedAt is not null, t?.Error, t?.ModelId, t?.CostUsd ?? 0m,
            t?.GeneratedAt, t?.ReviewedAt, t?.PublishedAt);
}
