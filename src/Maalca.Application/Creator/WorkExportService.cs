using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Creator;

/// <summary>
/// Exporta una obra como EPUB 3 a partir de los capítulos que ya están en la plataforma (EpubBuilder).
///
/// Quién puede: el autor dueño de la obra, o el sello con el que sale (Work.ImprintId), porque preparar la
/// edición es trabajo del editor. Un idioma por archivo: "es" es el original; "en" usa solo las traducciones
/// publicadas y exige que estén todas (un libro a medio traducir no se entrega).
///
/// La revisión previa (Preflight) dice lo que bloquea (error) y lo que conviene arreglar (aviso) antes de
/// subir el libro a una tienda. Build vuelve a correr la misma revisión y se niega si hay errores.
///
/// La portada se descarga del almacenamiento (CoverUrl / CoverUrlEn). Falla abierta: si no se puede bajar,
/// el libro sale sin portada y la revisión lo avisa. Solo https y nunca direcciones privadas (SSRF).
/// </summary>
public class WorkExportService : IWorkExportService
{
    public const string HttpClientName = "work-export";
    private const long MaxCoverBytes = 15 * 1024 * 1024;

    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _http;

    public WorkExportService(AppDbContext db, IHttpClientFactory http)
    {
        _db = db;
        _http = http;
    }

    public async Task<WorkExportPreflightDto?> PreflightAsync(Guid affiliateId, Guid workId, string? language, CancellationToken ct = default)
    {
        var plan = await PlanAsync(affiliateId, workId, language, ct);
        return plan?.Preflight;
    }

    public async Task<WorkExportFileDto?> BuildEpubAsync(Guid affiliateId, Guid workId, string? language, CancellationToken ct = default)
    {
        var plan = await PlanAsync(affiliateId, workId, language, ct);
        if (plan is null) return null;
        if (!plan.Preflight.Ready)
            throw new ArgumentException("EPUB: " + string.Join(" ", plan.Preflight.Issues.Where(i => i.Level == "error").Select(i => i.Message)));

        var bytes = EpubBuilder.Build(plan.Book);
        return new WorkExportFileDto(bytes, plan.FileName, EpubBuilder.MediaType);
    }

    // ── Plan: lee todo, revisa y arma el libro en memoria ─────────────────────────

    private sealed record ExportPlan(WorkExportPreflightDto Preflight, EpubBook Book, string FileName);

    private async Task<ExportPlan?> PlanAsync(Guid affiliateId, Guid workId, string? language, CancellationToken ct)
    {
        var work = await _db.Works.AsNoTracking()
            .Include(w => w.Affiliate)
            .FirstOrDefaultAsync(w => w.Id == workId && (w.AffiliateId == affiliateId || w.ImprintId == affiliateId), ct);
        if (work is null) return null;

        var lang = language?.Trim().ToLowerInvariant() is "en" ? "en" : "es";
        var issues = new List<WorkExportIssueDto>();
        void Error(string code, string msg) => issues.Add(new WorkExportIssueDto("error", code, msg));
        void Warn(string code, string msg) => issues.Add(new WorkExportIssueDto("warning", code, msg));

        var chapters = await _db.WorkChapters.AsNoTracking()
            .Where(c => c.WorkId == work.Id)
            .OrderBy(c => c.Number)
            .Select(c => new { c.Id, c.Number, c.Title, c.ContentHtml, c.WordCount })
            .ToListAsync(ct);

        var translations = await _db.WorkChapterTranslations.AsNoTracking()
            .Where(t => t.WorkId == work.Id && t.Status == "published" && t.Title != null)
            .Select(t => new { t.ChapterId, t.Language, t.Title, t.ContentHtml, t.WordCount, t.SourceHash })
            .ToListAsync(ct);
        var languages = new[] { "es" }
            .Concat(translations.Select(t => t.Language).Distinct().OrderBy(l => l))
            .ToList();

        if (chapters.Count == 0) Error("no_chapters", "La obra no tiene capítulos.");

        // Texto por capítulo en el idioma pedido.
        var parts = new List<(int Number, string Title, string? Html)>();
        var untranslated = new List<int>();
        var stale = new List<int>();
        foreach (var c in chapters)
        {
            if (lang == "es")
            {
                parts.Add((c.Number, c.Title, c.ContentHtml));
                continue;
            }
            var t = translations.FirstOrDefault(x => x.ChapterId == c.Id && x.Language == lang);
            if (t is null) { untranslated.Add(c.Number); continue; }
            if (t.SourceHash is not null && t.SourceHash != WorkTranslationService.Hash(c.Title, c.ContentHtml)) stale.Add(c.Number);
            parts.Add((c.Number, t.Title!, t.ContentHtml));
        }
        if (untranslated.Count > 0)
            Error("untranslated", $"Faltan {untranslated.Count} de {chapters.Count} capítulos por traducir y publicar en inglés ({Numbers(untranslated)}).");
        if (stale.Count > 0)
            Warn("stale_translation", $"Cambiaste el original después de revisar la traducción de {stale.Count} capítulo(s) ({Numbers(stale)}).");

        var empty = parts.Where(p => string.IsNullOrWhiteSpace(Tags.Replace(p.Html ?? string.Empty, string.Empty).Replace("&nbsp;", " ")))
            .Select(p => p.Number).ToList();
        if (chapters.Count > 0 && empty.Count == parts.Count && parts.Count > 0) Error("all_empty", "Todos los capítulos están vacíos.");
        else if (empty.Count > 0) Warn("empty_chapters", $"Hay {empty.Count} capítulo(s) sin texto ({Numbers(empty)}).");

        var epubChapters = new List<EpubChapter>();
        var droppedImages = 0;
        foreach (var p in parts)
        {
            var x = XhtmlFragment.Convert(p.Html);
            droppedImages += x.DroppedImages;
            epubChapters.Add(new EpubChapter(p.Title, x.Xhtml));
        }
        if (droppedImages > 0)
            Warn("images_dropped", $"El texto trae {droppedImages} imagen(es) que no entran en el EPUB; se quitaron.");

        // Metadatos de la edición.
        var title = lang == "en" && !string.IsNullOrWhiteSpace(work.TitleEn) ? work.TitleEn! : work.Title;
        if (lang == "en" && string.IsNullOrWhiteSpace(work.TitleEn))
            Warn("no_title_en", "No hay título en inglés; el libro sale con el título en español.");
        var synopsis = lang == "en" ? work.SynopsisEn : work.Synopsis;
        if (string.IsNullOrWhiteSpace(synopsis))
            Warn("no_synopsis", lang == "en" ? "Falta la sinopsis en inglés (la descripción que leen las tiendas)." : "Falta la sinopsis (la descripción que leen las tiendas).");
        var author = work.Byline ?? work.Affiliate?.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(work.Byline))
            Warn("no_byline", $"No pusiste el nombre del autor como sale en el libro; se usa \"{author}\".");

        string? publisher = work.Imprint;
        if (work.ImprintId is Guid imprintId)
            publisher = await _db.Affiliates.AsNoTracking().Where(a => a.Id == imprintId).Select(a => a.Name).FirstOrDefaultAsync(ct) ?? publisher;
        if (string.IsNullOrWhiteSpace(publisher)) Warn("no_imprint", "La obra no tiene sello; los créditos salen sin editorial.");

        // El ISBN es de una edición: el de la ficha es el de la edición en español.
        var isbn = lang == "es" ? NormalizeIsbn(work.Isbn) : null;
        if (lang == "es" && string.IsNullOrWhiteSpace(work.Isbn))
            Warn("no_isbn", "Sin ISBN. Para vender en tiendas con Duende como editorial hace falta uno propio por edición.");
        else if (lang == "es" && isbn is null)
            Warn("bad_isbn", "El ISBN de la ficha no es válido (13 dígitos con el dígito de control correcto); no se incluye.");
        if (lang == "en")
            Warn("no_isbn_en", "La edición en inglés lleva su propio ISBN; por ahora sale sin él.");

        string? credit = null;
        if (lang != "es")
        {
            credit = await _db.WorkTranslationSettings.AsNoTracking()
                .Where(s => s.WorkId == work.Id && s.Language == lang)
                .Select(s => s.Credit)
                .FirstOrDefaultAsync(ct);
            credit = string.IsNullOrWhiteSpace(credit) ? "Translated by the author" : credit;
        }

        // Portada del idioma (la inglesa si existe; si no, la española con aviso).
        var coverUrl = lang == "en" ? work.CoverUrlEn ?? work.CoverUrl : work.CoverUrl;
        if (lang == "en" && work.CoverUrlEn is null && work.CoverUrl is not null)
            Warn("cover_es_in_en", "No hay portada en inglés; se usa la española.");
        EpubImage? cover = null;
        if (coverUrl is null) Warn("no_cover", "Falta la portada.");
        else
        {
            var (img, problem) = await DownloadCoverAsync(coverUrl, ct);
            cover = img;
            if (problem is not null) Warn("cover_unavailable", problem);
        }

        if (work.Status != "published")
            Warn("draft", "La obra está en borrador en tu página. El EPUB sale igual (sirve para revisarlo).");

        var preflight = new WorkExportPreflightDto(
            lang, languages, !issues.Any(i => i.Level == "error"), issues,
            title, author, publisher, isbn, parts.Count, parts.Sum(p => WordCount(p.Html)));

        var book = new EpubBook(
            Identifier: isbn is not null ? "urn:isbn:" + isbn : "urn:uuid:" + StableGuid(work.Id, lang),
            Isbn: isbn is not null ? FormatIsbn(isbn) : null,
            Title: title,
            Subtitle: lang == "es" ? work.Subtitle : null,
            Language: lang,
            Author: author,
            Publisher: publisher,
            Year: work.Year,
            Description: PlainText(synopsis),
            TranslationCredit: credit,
            OriginalTitle: lang == "es" ? null : work.Title,
            Poetry: work.Kind == "poetry",
            Cover: cover,
            Chapters: epubChapters,
            ModifiedUtc: DateTime.UtcNow);

        var fileName = work.Slug + (lang == "es" ? "" : "-" + lang) + ".epub";
        return new ExportPlan(preflight, book, fileName);
    }

    // ── Portada ──────────────────────────────────────────────────────────────────

    private async Task<(EpubImage? Image, string? Problem)> DownloadCoverAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !await IsPublicHostAsync(uri.Host))
            return (null, "La portada no está en una dirección https pública; el libro sale sin portada.");
        try
        {
            var client = _http.CreateClient(HttpClientName);
            using var res = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode) return (null, $"No se pudo bajar la portada (HTTP {(int)res.StatusCode}); el libro sale sin portada.");
            if (res.Content.Headers.ContentLength is > MaxCoverBytes) return (null, "La portada pesa más de 15 MB; el libro sale sin portada.");

            await using var s = await res.Content.ReadAsStreamAsync(ct);
            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await s.ReadAsync(buffer, ct)) > 0)
            {
                ms.Write(buffer, 0, read);
                if (ms.Length > MaxCoverBytes) return (null, "La portada pesa más de 15 MB; el libro sale sin portada.");
            }
            var bytes = ms.ToArray();
            var kind = Sniff(bytes);
            if (kind is null) return (null, "La portada no es JPG, PNG, GIF ni WebP; el libro sale sin portada.");
            return (new EpubImage(bytes, kind.Value.MediaType, kind.Value.Extension), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return (null, "No se pudo bajar la portada a tiempo; el libro sale sin portada.");
        }
    }

    // El tipo real por los primeros bytes (no por la extensión ni el Content-Type, que pueden mentir).
    private static (string MediaType, string Extension)? Sniff(byte[] b)
    {
        if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ("image/jpeg", "jpg");
        if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ("image/png", "png");
        if (b.Length > 6 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return ("image/gif", "gif");
        if (b.Length > 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46
            && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return ("image/webp", "webp");
        return null;
    }

    private static async Task<bool> IsPublicHostAsync(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            return false;
        IPAddress[] addresses;
        try { addresses = IPAddress.TryParse(host, out var ip) ? new[] { ip } : await Dns.GetHostAddressesAsync(host); }
        catch (SocketException) { return false; }
        return addresses.Length > 0 && addresses.All(IsPublicAddress);
    }

    private static bool IsPublicAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return !(ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal || ip.Equals(IPAddress.IPv6Any));
        var b = ip.GetAddressBytes();
        return !(b[0] == 10 || b[0] == 127 || b[0] == 0
                 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                 || (b[0] == 192 && b[1] == 168)
                 || (b[0] == 169 && b[1] == 254)
                 || (b[0] == 100 && b[1] >= 64 && b[1] <= 127));
    }

    // ── Apoyo ────────────────────────────────────────────────────────────────────

    /// <summary>ISBN-13 válido (dígito de control incluido) sin guiones, o null.</summary>
    public static string? NormalizeIsbn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length != 13) return null;
        var sum = 0;
        for (var i = 0; i < 12; i++) sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        var check = (10 - sum % 10) % 10;
        return check == digits[12] - '0' ? digits : null;
    }

    private static string FormatIsbn(string isbn13) => isbn13; // sin guiones: la partición depende del rango del país

    // Mismo identificador cada vez que se exporta la misma obra en el mismo idioma (las tiendas lo usan
    // para reconocer una actualización del mismo libro).
    private static Guid StableGuid(Guid workId, string lang)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(workId.ToString("N") + ":" + lang));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30); // versión 3 (basado en nombre)
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash);
    }

    private static string? PlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var text = WebUtility.HtmlDecode(Tags.Replace(html.Replace("</p>", "\n").Replace("<br", "\n<br"), " "));
        text = Spaces.Replace(text, " ").Trim();
        return text.Length == 0 ? null : text;
    }

    private static int WordCount(string? html) => Maalca.Application.Services.WorkService.CountWords(html);

    private static string Numbers(List<int> numbers)
        => numbers.Count <= 8 ? string.Join(", ", numbers) : string.Join(", ", numbers.Take(8)) + "…";
}
