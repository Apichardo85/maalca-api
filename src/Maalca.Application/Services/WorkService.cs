using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Application.Creator;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>
/// Obras del espacio de autor (BusinessType.Creator; Publisher también, para el sello). Mismo patrón que
/// VehicleListingService: CRUD por afiliado + lectura pública. La regla de qué capítulo se puede leer
/// vive aquí (CanRead) y solo aquí: la web pinta lo que el servidor le manda.
/// </summary>
public class WorkService : IWorkService
{
    private static readonly string[] Kinds = { "novel", "poetry", "stories", "micro", "essay", "chronicle", "other" };
    private static readonly string[] Statuses = { "draft", "published" };
    private static readonly string[] Accesses = { "free", "sample", "members", "closed" };
    private static readonly BusinessType[] AuthorTypes = { BusinessType.Creator, BusinessType.Publisher };

    private const int MaxBuyLinks = 8;
    private const int MaxChapters = 500;
    // Un capítulo de novela larga cabe sobrado; evita que un pegado accidental de un libro entero
    // en un solo capítulo infle la respuesta pública.
    private const int MaxChapterChars = 400_000;

    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Words = new(@"[\p{L}\p{N}]+(?:['’-][\p{L}\p{N}]+)*", RegexOptions.Compiled);

    private readonly AppDbContext _db;

    public WorkService(AppDbContext db) => _db = db;

    // ── Dashboard: obras ─────────────────────────────────────────────────────────

    public async Task<List<WorkDto>> GetAsync(Guid affiliateId)
    {
        var works = await _db.Works.AsNoTracking()
            .Where(w => w.AffiliateId == affiliateId)
            .OrderBy(w => w.SortOrder).ThenByDescending(w => w.CreatedAt)
            .ToListAsync();
        var stats = await ChapterStatsAsync(works.Select(w => w.Id).ToList());
        return works.Select(w => ToDto(w, stats)).ToList();
    }

    public async Task<WorkDetailDto?> GetDetailAsync(Guid affiliateId, Guid workId)
    {
        var work = await _db.Works.AsNoTracking().FirstOrDefaultAsync(w => w.Id == workId && w.AffiliateId == affiliateId);
        if (work is null) return null;
        var chapters = await ChapterRefsAsync(workId);
        var stats = new Dictionary<Guid, (int, int)> { [workId] = (chapters.Count, chapters.Sum(c => c.WordCount)) };
        return new WorkDetailDto(ToDto(work, stats), chapters);
    }

    public async Task<WorkDto> CreateAsync(Guid affiliateId, UpsertWorkRequest r)
    {
        var work = new Work { Id = Guid.NewGuid(), AffiliateId = affiliateId, CreatedAt = DateTime.UtcNow };
        Apply(work, r);
        work.Slug = await UniqueSlugAsync(affiliateId, r.Slug, work.Title, null);
        _db.Works.Add(work);
        await _db.SaveChangesAsync();
        return ToDto(work, new Dictionary<Guid, (int, int)>());
    }

    public async Task<WorkDto?> UpdateAsync(Guid affiliateId, Guid workId, UpsertWorkRequest r)
    {
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == workId && w.AffiliateId == affiliateId);
        if (work is null) return null;
        Apply(work, r);
        // El slug solo cambia si el dueño lo pide: cambiarlo rompe los enlaces que ya compartió.
        if (!string.IsNullOrWhiteSpace(r.Slug) && Slugify(r.Slug) != work.Slug)
            work.Slug = await UniqueSlugAsync(affiliateId, r.Slug, work.Title, work.Id);
        work.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(work, await ChapterStatsAsync(new List<Guid> { work.Id }));
    }

    public async Task<bool> DeleteAsync(Guid affiliateId, Guid workId)
    {
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == workId && w.AffiliateId == affiliateId);
        if (work is null) return false;
        _db.Works.Remove(work); // capítulos por cascada
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Dashboard: capítulos ─────────────────────────────────────────────────────

    public async Task<WorkChapterDto?> GetChapterAsync(Guid affiliateId, Guid workId, Guid chapterId)
    {
        var ch = await _db.WorkChapters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chapterId && c.WorkId == workId && c.Work!.AffiliateId == affiliateId);
        return ch is null ? null : ToChapterDto(ch);
    }

    public async Task<WorkChapterDto?> CreateChapterAsync(Guid affiliateId, Guid workId, UpsertWorkChapterRequest r)
    {
        if (!await OwnsWorkAsync(affiliateId, workId)) return null;

        var numbers = await _db.WorkChapters.Where(c => c.WorkId == workId).Select(c => c.Number).ToListAsync();
        if (numbers.Count >= MaxChapters) throw new ArgumentException($"Obra: máximo {MaxChapters} capítulos.");
        var next = numbers.Count == 0 ? 1 : numbers.Max() + 1;

        var ch = new WorkChapter { Id = Guid.NewGuid(), WorkId = workId, CreatedAt = DateTime.UtcNow };
        ApplyChapter(ch, r);

        if (r.Number is int wanted && wanted >= 1 && wanted < next)
        {
            // Insertar en medio: corre los de wanted en adelante. Dos pasos por el índice único (WorkId, Number).
            await ShiftNumbersAsync(workId, fromNumber: wanted, delta: 1);
            ch.Number = wanted;
        }
        else
        {
            ch.Number = next;
        }

        _db.WorkChapters.Add(ch);
        await TouchWorkAsync(workId);
        await _db.SaveChangesAsync();
        return ToChapterDto(ch);
    }

    public async Task<WorkChapterDto?> UpdateChapterAsync(Guid affiliateId, Guid workId, Guid chapterId, UpsertWorkChapterRequest r)
    {
        var ch = await _db.WorkChapters
            .FirstOrDefaultAsync(c => c.Id == chapterId && c.WorkId == workId && c.Work!.AffiliateId == affiliateId);
        if (ch is null) return null;
        ApplyChapter(ch, r);
        ch.UpdatedAt = DateTime.UtcNow;
        // El orden se cambia con ReorderChaptersAsync, no aquí: así no hay dos caminos para lo mismo.
        await TouchWorkAsync(workId);
        await _db.SaveChangesAsync();
        return ToChapterDto(ch);
    }

    public async Task<bool> DeleteChapterAsync(Guid affiliateId, Guid workId, Guid chapterId)
    {
        var ch = await _db.WorkChapters
            .FirstOrDefaultAsync(c => c.Id == chapterId && c.WorkId == workId && c.Work!.AffiliateId == affiliateId);
        if (ch is null) return false;
        var removed = ch.Number;
        _db.WorkChapters.Remove(ch);
        await _db.SaveChangesAsync();
        // Cierra el hueco para que la numeración pública siga 1..N.
        await ShiftNumbersAsync(workId, fromNumber: removed + 1, delta: -1);
        await TouchWorkAsync(workId);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<WorkChapterRefDto>?> ReorderChaptersAsync(Guid affiliateId, Guid workId, ReorderWorkChaptersRequest r)
    {
        if (!await OwnsWorkAsync(affiliateId, workId)) return null;

        var chapters = await _db.WorkChapters.Where(c => c.WorkId == workId).ToListAsync();
        var ids = r.ChapterIds ?? Array.Empty<Guid>();
        if (ids.Count != chapters.Count || ids.Distinct().Count() != ids.Count || ids.Any(id => chapters.All(c => c.Id != id)))
            throw new ArgumentException("Obra: el nuevo orden debe incluir todos los capítulos, una vez cada uno.");

        // Dos pasos por el índice único: primero a negativos, después al número final.
        await using var tx = await _db.Database.BeginTransactionAsync();
        foreach (var c in chapters) c.Number = -c.Number;
        await _db.SaveChangesAsync();
        for (var i = 0; i < ids.Count; i++)
            chapters.First(c => c.Id == ids[i]).Number = i + 1;
        await TouchWorkAsync(workId);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return await ChapterRefsAsync(workId);
    }

    public async Task<IReadOnlyList<WorkChapterRefDto>?> ImportChaptersAsync(Guid affiliateId, Guid workId, ImportWorkChaptersRequest r)
    {
        if (!await OwnsWorkAsync(affiliateId, workId)) return null;
        var incoming = r.Chapters ?? Array.Empty<UpsertWorkChapterRequest>();
        if (incoming.Count == 0) throw new ArgumentException("El manuscrito no trae capítulos.");
        var replace = string.Equals(r.Mode, "replace", StringComparison.OrdinalIgnoreCase);

        var existing = await _db.WorkChapters.Where(c => c.WorkId == workId).ToListAsync();
        var start = replace || existing.Count == 0 ? 1 : existing.Max(c => c.Number) + 1;
        if ((replace ? 0 : existing.Count) + incoming.Count > MaxChapters)
            throw new ArgumentException($"Obra: máximo {MaxChapters} capítulos.");

        // Validar todo antes de tocar nada: un capítulo malo no deja la obra a medias.
        var now = DateTime.UtcNow;
        var built = incoming.Select((c, i) =>
        {
            var ch = new WorkChapter { Id = Guid.NewGuid(), WorkId = workId, Number = start + i, CreatedAt = now };
            try { ApplyChapter(ch, c); }
            catch (ArgumentException ex) { throw new ArgumentException($"Capítulo {i + 1}: {ex.Message}"); }
            return ch;
        }).ToList();

        await using var tx = await _db.Database.BeginTransactionAsync();
        if (replace && existing.Count > 0)
        {
            _db.WorkChapters.RemoveRange(existing);
            await _db.SaveChangesAsync();
        }
        _db.WorkChapters.AddRange(built);
        await TouchWorkAsync(workId);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return await ChapterRefsAsync(workId);
    }

    // ── Página pública ───────────────────────────────────────────────────────────

    public async Task<List<PublicWorkSummaryDto>?> GetPublicAsync(string affiliateSlug, ReaderContext? reader = null)
    {
        var affiliateId = await PublicAuthorIdAsync(affiliateSlug);
        if (affiliateId is null) return null;

        var works = await _db.Works.AsNoTracking()
            .Where(w => w.AffiliateId == affiliateId && w.Status == "published")
            .OrderByDescending(w => w.Featured).ThenBy(w => w.SortOrder).ThenByDescending(w => w.Year).ThenByDescending(w => w.CreatedAt)
            .ToListAsync();
        var workIds = works.Select(w => w.Id).ToList();
        var chapters = await _db.WorkChapters.AsNoTracking()
            .Where(c => workIds.Contains(c.WorkId))
            .Select(c => new { c.WorkId, c.IsSample })
            .ToListAsync();

        var now = DateTime.UtcNow;
        return works.Select(w =>
        {
            var mine = chapters.Where(c => c.WorkId == w.Id).ToList();
            var readable = mine.Count(c => CanRead(w, c.IsSample, now, reader?.IsMember == true));
            return ToPublicSummary(w, mine.Count, readable, now);
        }).ToList();
    }

    public async Task<PublicWorkDto?> GetPublicWorkAsync(string affiliateSlug, string workSlug, ReaderContext? reader = null)
    {
        var work = await PublicWorkAsync(affiliateSlug, workSlug);
        if (work is null) return null;

        var now = DateTime.UtcNow;
        var chapters = await _db.WorkChapters.AsNoTracking()
            .Where(c => c.WorkId == work.Id)
            .OrderBy(c => c.Number)
            .Select(c => new { c.Number, c.Title, c.WordCount, c.IsSample })
            .ToListAsync();
        var member = reader?.IsMember == true;
        var refs = chapters
            .Select(c =>
            {
                var readable = CanRead(work, c.IsSample, now, member);
                var forMembers = !readable && work.ReadingAccess == "members";
                return new PublicWorkChapterRefDto(c.Number, c.Title, c.WordCount, readable, forMembers);
            })
            .ToList();
        var summary = ToPublicSummary(work, refs.Count, refs.Count(c => c.Readable), now);

        int? continueAt = null;
        if (reader is not null)
        {
            var at = await _db.ReaderProgress.AsNoTracking()
                .Where(p => p.ReaderId == reader.ReaderId && p.WorkId == work.Id)
                .Select(p => (int?)p.ChapterNumber)
                .FirstOrDefaultAsync();
            if (at is int n && refs.Any(c => c.Number == n && c.Readable)) continueAt = n;
        }
        return new PublicWorkDto(summary, work.Isbn, work.ContentNotice, refs, continueAt);
    }

    public async Task<PublicWorkChapterDto?> GetPublicChapterAsync(string affiliateSlug, string workSlug, int number, ReaderContext? reader = null)
    {
        var work = await PublicWorkAsync(affiliateSlug, workSlug);
        if (work is null) return null;

        var ch = await _db.WorkChapters.AsNoTracking().FirstOrDefaultAsync(c => c.WorkId == work.Id && c.Number == number);
        var now = DateTime.UtcNow;
        var member = reader?.IsMember == true;
        if (ch is null || !CanRead(work, ch.IsSample, now, member)) return null;

        // Anterior/siguiente solo entre los que también se pueden leer: el lector nunca lleva a un 404.
        var readableNumbers = (await _db.WorkChapters.AsNoTracking()
                .Where(c => c.WorkId == work.Id)
                .Select(c => new { c.Number, c.IsSample })
                .ToListAsync())
            .Where(c => CanRead(work, c.IsSample, now, member))
            .Select(c => c.Number)
            .OrderBy(n => n)
            .ToList();
        int? prev = readableNumbers.Where(n => n < number).Select(n => (int?)n).LastOrDefault();
        int? next = readableNumbers.Where(n => n > number).Select(n => (int?)n).FirstOrDefault();

        return new PublicWorkChapterDto(work.Slug, work.Title, ch.Number, ch.Title, ch.ContentHtml, ch.WordCount, prev, next, work.ContentNotice);
    }

    public async Task<List<SitemapWorkDto>> GetSitemapAsync()
        => await _db.Works.AsNoTracking()
            .Where(w => w.Status == "published" && !w.IsDemo
                        && w.Affiliate!.Published && AuthorTypes.Contains(w.Affiliate.BusinessType))
            .OrderBy(w => w.Affiliate!.Slug).ThenBy(w => w.SortOrder)
            .Take(5000)
            .Select(w => new SitemapWorkDto(w.Affiliate!.Slug, w.Slug, w.UpdatedAt ?? w.CreatedAt))
            .ToListAsync();

    // ── Regla de lectura ─────────────────────────────────────────────────────────

    /// <summary>
    /// La ventana gratis abre todo; fuera de ella manda ReadingAccess. "members" deja leer las muestras
    /// a cualquiera y la obra completa a los miembros activos del club (ReaderClubService.IsMember).
    /// </summary>
    public static bool CanRead(Work w, bool isSample, DateTime nowUtc, bool isMember = false)
        => InFreeWindow(w, nowUtc)
           || w.ReadingAccess == "free"
           || ((w.ReadingAccess == "sample" || w.ReadingAccess == "members") && isSample)
           || (w.ReadingAccess == "members" && isMember);

    private static bool InFreeWindow(Work w, DateTime nowUtc)
        => (w.FreeFrom.HasValue || w.FreeUntil.HasValue)
           && (!w.FreeFrom.HasValue || w.FreeFrom.Value <= nowUtc)
           && (!w.FreeUntil.HasValue || w.FreeUntil.Value > nowUtc);

    // ── Apoyo ────────────────────────────────────────────────────────────────────

    private async Task<Guid?> PublicAuthorIdAsync(string slug)
        => await _db.Affiliates.AsNoTracking()
            .Where(a => a.Slug == slug && a.Published && AuthorTypes.Contains(a.BusinessType))
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync();

    private async Task<Work?> PublicWorkAsync(string affiliateSlug, string workSlug)
    {
        var affiliateId = await PublicAuthorIdAsync(affiliateSlug);
        if (affiliateId is null) return null;
        var slug = workSlug.Trim().ToLowerInvariant();
        return await _db.Works.AsNoTracking()
            .FirstOrDefaultAsync(w => w.AffiliateId == affiliateId && w.Slug == slug && w.Status == "published");
    }

    private Task<bool> OwnsWorkAsync(Guid affiliateId, Guid workId)
        => _db.Works.AnyAsync(w => w.Id == workId && w.AffiliateId == affiliateId);

    private async Task TouchWorkAsync(Guid workId)
    {
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == workId);
        if (work is not null) work.UpdatedAt = DateTime.UtcNow;
    }

    private async Task ShiftNumbersAsync(Guid workId, int fromNumber, int delta)
    {
        var rows = await _db.WorkChapters.Where(c => c.WorkId == workId && c.Number >= fromNumber).ToListAsync();
        if (rows.Count == 0) return;
        foreach (var c in rows) c.Number = -(c.Number + delta);
        await _db.SaveChangesAsync();
        foreach (var c in rows) c.Number = -c.Number;
        await _db.SaveChangesAsync();
    }

    private async Task<Dictionary<Guid, (int Count, int Words)>> ChapterStatsAsync(List<Guid> workIds)
    {
        if (workIds.Count == 0) return new();
        return await _db.WorkChapters.AsNoTracking()
            .Where(c => workIds.Contains(c.WorkId))
            .GroupBy(c => c.WorkId)
            .Select(g => new { g.Key, Count = g.Count(), Words = g.Sum(c => c.WordCount) })
            .ToDictionaryAsync(x => x.Key, x => (x.Count, x.Words));
    }

    private async Task<List<WorkChapterRefDto>> ChapterRefsAsync(Guid workId)
        => await _db.WorkChapters.AsNoTracking()
            .Where(c => c.WorkId == workId)
            .OrderBy(c => c.Number)
            .Select(c => new WorkChapterRefDto(c.Id, c.Number, c.Title, c.IsSample, c.WordCount, c.UpdatedAt))
            .ToListAsync();

    private async Task<string> UniqueSlugAsync(Guid affiliateId, string? requested, string title, Guid? excludeId)
    {
        var baseSlug = Slugify(string.IsNullOrWhiteSpace(requested) ? title : requested);
        if (baseSlug.Length == 0) baseSlug = "obra";
        var taken = await _db.Works.AsNoTracking()
            .Where(w => w.AffiliateId == affiliateId && w.Id != excludeId && w.Slug.StartsWith(baseSlug))
            .Select(w => w.Slug)
            .ToListAsync();
        if (!taken.Contains(baseSlug)) return baseSlug;
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    public static string Slugify(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var dash = false;
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(ch);
                dash = false;
            }
            else if (!dash && sb.Length > 0)
            {
                sb.Append('-');
                dash = true;
            }
        }
        var s = sb.ToString().Trim('-');
        return s.Length > 100 ? s[..100].Trim('-') : s;
    }

    private static void Apply(Work w, UpsertWorkRequest r)
    {
        var title = Clip(r.Title, 200) ?? throw new ArgumentException("Obra: el título es obligatorio.");
        if (r.Year is < 1000 or > 2200) throw new ArgumentException("Obra: el año no es válido.");
        if (r.FreeFrom.HasValue && r.FreeUntil.HasValue && r.FreeUntil <= r.FreeFrom)
            throw new ArgumentException("Obra: la lectura gratis debe terminar después de empezar.");

        w.Title = title;
        w.TitleEn = Clip(r.TitleEn, 200);
        w.Subtitle = Clip(r.Subtitle, 200);
        w.Kind = Token(r.Kind, Kinds, "novel", "tipo");
        w.Status = Token(r.Status, Statuses, "draft", "estado");
        w.Synopsis = HtmlScrub.SanitizeRichText(Clip(r.Synopsis, 20_000));
        w.SynopsisEn = HtmlScrub.SanitizeRichText(Clip(r.SynopsisEn, 20_000));
        w.CoverUrl = HttpUrl(Clip(r.CoverUrl, 500));
        w.Year = r.Year;
        w.Isbn = Clip(r.Isbn, 20);
        w.Imprint = Clip(r.Imprint, 120);
        w.ContentNotice = Clip(r.ContentNotice, 300);
        w.ReadingAccess = Token(r.ReadingAccess, Accesses, "sample", "acceso de lectura");
        w.FreeFrom = ToUtc(r.FreeFrom);
        w.FreeUntil = ToUtc(r.FreeUntil);
        w.Featured = r.Featured;
        w.SortOrder = r.SortOrder;

        var links = (r.BuyLinks ?? Array.Empty<WorkBuyLinkDto>())
            .Select(l => new WorkBuyLinkDto(Clip(l.Label, 60) ?? string.Empty, HttpUrl(Clip(l.Url, 500)) ?? string.Empty))
            .Where(l => l.Label.Length > 0 && l.Url.Length > 0)
            .Take(MaxBuyLinks)
            .ToList();
        w.BuyLinks = links.Count == 0 ? null : JsonArrayField.Serialize(links);
    }

    private static void ApplyChapter(WorkChapter c, UpsertWorkChapterRequest r)
    {
        c.Title = Clip(r.Title, 200) ?? throw new ArgumentException("Capítulo: el título es obligatorio.");
        if (r.ContentHtml is { Length: > MaxChapterChars })
            throw new ArgumentException("Capítulo: el texto es demasiado largo; divídelo en varios capítulos.");
        c.ContentHtml = HtmlScrub.SanitizeRichText(r.ContentHtml);
        c.IsSample = r.IsSample;
        c.WordCount = CountWords(c.ContentHtml);
    }

    public static int CountWords(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return 0;
        var text = System.Net.WebUtility.HtmlDecode(Tags.Replace(html, " "));
        return Words.Matches(text).Count;
    }

    private static WorkDto ToDto(Work w, IReadOnlyDictionary<Guid, (int Count, int Words)> stats)
    {
        stats.TryGetValue(w.Id, out var s);
        return new WorkDto(
            w.Id, w.Title, w.TitleEn, w.Subtitle, w.Slug, w.Kind, w.Status, w.Synopsis, w.SynopsisEn, w.CoverUrl,
            w.Year, w.Isbn, w.Imprint, w.ContentNotice, JsonArrayField.Parse<WorkBuyLinkDto>(w.BuyLinks),
            w.ReadingAccess, w.FreeFrom, w.FreeUntil, w.Featured, w.SortOrder, w.IsDemo,
            s.Count, s.Words, w.CreatedAt, w.UpdatedAt);
    }

    private static PublicWorkSummaryDto ToPublicSummary(Work w, int chapterCount, int readableCount, DateTime now)
    {
        var readable = chapterCount == 0 || readableCount == 0 ? "none" : readableCount == chapterCount ? "full" : "sample";
        return new PublicWorkSummaryDto(
            w.Id, w.Slug, w.Title, w.TitleEn, w.Subtitle, w.Kind, w.Synopsis, w.SynopsisEn, w.CoverUrl,
            w.Year, w.Imprint, w.Featured, chapterCount, readable,
            InFreeWindow(w, now) ? w.FreeUntil : null,
            JsonArrayField.Parse<WorkBuyLinkDto>(w.BuyLinks),
            w.ReadingAccess == "members");
    }

    private static WorkChapterDto ToChapterDto(WorkChapter c)
        => new(c.Id, c.WorkId, c.Number, c.Title, c.ContentHtml, c.IsSample, c.WordCount);

    private static DateTime? ToUtc(DateTime? d) => d?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => d,
        DateTimeKind.Local => d.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(d!.Value, DateTimeKind.Utc),
    };

    private static string? HttpUrl(string? url)
        => url is not null && Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp)
            ? url
            : null;

    private static string? Clip(string? value, int max)
    {
        var t = value?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > max ? t[..max] : t;
    }

    private static string Token(string? value, string[] allowed, string fallback, string label)
    {
        var t = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(t)) return fallback;
        if (!allowed.Contains(t)) throw new ArgumentException($"Obra: {label} no válido.");
        return t;
    }
}
