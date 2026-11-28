using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Creator;

/// <summary>
/// Sello editorial: contratos autor ↔ sello y el catálogo del editor. Ver ImprintAuthor.cs para el flujo.
///
/// Decisiones:
/// - El autor se busca por la dirección de su espacio solo para encontrarlo; lo que se guarda es su llave.
/// - Los términos (regalías, derechos, fechas) solo se cambian mientras el contrato está en invitación:
///   cambiar lo que el autor ya aceptó sin que lo vuelva a aceptar no se permite. Para renegociar un contrato
///   activo se termina y se invita de nuevo.
/// - Terminar desenlaza las obras del autor en el mismo guardado: Work.ImprintId nunca apunta a un sello
///   sin contrato activo, así la página pública y el EPUB no tienen que volver a comprobarlo.
/// </summary>
public class ImprintService : IImprintService
{
    public static readonly string[] RightsAllowed = { "ebook", "print", "audio", "translation" };
    private static readonly string[] OpenStatuses = { "invited", "active" };

    private readonly AppDbContext _db;

    public ImprintService(AppDbContext db) => _db = db;

    // ── Lado del sello ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ImprintContractDto>> GetAuthorsAsync(Guid imprintId)
    {
        await RequirePublisherAsync(imprintId);
        var rows = await ContractsQuery().Where(c => c.ImprintId == imprintId)
            .OrderBy(c => c.Status == "active" ? 0 : c.Status == "invited" ? 1 : 2).ThenBy(c => c.Author!.Name)
            .ToListAsync();
        return await ToDtosAsync(rows);
    }

    public async Task<ImprintContractDto> InviteAsync(Guid imprintId, InviteImprintAuthorRequest r)
    {
        await RequirePublisherAsync(imprintId);
        var slug = NormalizeSlug(r.AuthorSlug);
        if (slug.Length == 0) throw new ArgumentException("Sello: escribe la dirección del espacio del autor (maalca.com/…).");

        var author = await _db.Affiliates.AsNoTracking()
            .Where(a => a.Slug == slug)
            .Select(a => new { a.Id, a.BusinessType })
            .FirstOrDefaultAsync()
            ?? throw new ArgumentException($"Sello: no hay ningún espacio en maalca.com/{slug}.");
        if (author.Id == imprintId) throw new ArgumentException("Sello: las obras del propio sello no necesitan contrato.");
        if (author.BusinessType != BusinessType.Creator)
            throw new ArgumentException("Sello: ese espacio no es de un autor.");

        var contract = await _db.ImprintAuthors.FirstOrDefaultAsync(c => c.ImprintId == imprintId && c.AuthorId == author.Id);
        if (contract is not null && OpenStatuses.Contains(contract.Status))
            throw new ArgumentException(contract.Status == "active"
                ? "Sello: ya hay un contrato activo con ese autor."
                : "Sello: ese autor ya tiene una invitación pendiente.");

        if (contract is null)
        {
            contract = new ImprintAuthor { Id = Guid.NewGuid(), ImprintId = imprintId, AuthorId = author.Id, CreatedAt = DateTime.UtcNow };
            _db.ImprintAuthors.Add(contract);
        }
        else
        {
            // Rechazado o terminado antes: se reabre como invitación nueva con los términos nuevos.
            contract.UpdatedAt = DateTime.UtcNow;
            contract.RespondedAt = null;
            contract.EndedAt = null;
        }
        ApplyTerms(contract, r.RoyaltyEbookPercent, r.RoyaltyPrintPercent, r.Rights, r.StartsOn, r.EndsOn, r.Notes);
        contract.Status = "invited";
        await _db.SaveChangesAsync();
        return (await GetContractAsync(contract.Id))!;
    }

    public async Task<ImprintContractDto?> UpdateTermsAsync(Guid imprintId, Guid contractId, UpdateImprintContractRequest r)
    {
        await RequirePublisherAsync(imprintId);
        var contract = await _db.ImprintAuthors.FirstOrDefaultAsync(c => c.Id == contractId && c.ImprintId == imprintId);
        if (contract is null) return null;
        if (contract.Status is "active")
            throw new ArgumentException("Sello: el autor ya aceptó estos términos. Para cambiarlos, termina el contrato e invítalo de nuevo.");
        if (contract.Status is "ended")
            throw new ArgumentException("Sello: el contrato está terminado. Invita al autor de nuevo.");

        ApplyTerms(contract, r.RoyaltyEbookPercent, r.RoyaltyPrintPercent, r.Rights, r.StartsOn, r.EndsOn, r.Notes);
        contract.Status = "invited";
        contract.RespondedAt = null;
        contract.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetContractAsync(contract.Id);
    }

    public async Task<EndImprintContractResultDto?> EndAsync(Guid imprintId, Guid contractId)
    {
        await RequirePublisherAsync(imprintId);
        var contract = await _db.ImprintAuthors.FirstOrDefaultAsync(c => c.Id == contractId && c.ImprintId == imprintId);
        if (contract is null) return null;
        if (contract.Status is "ended" or "declined") return new EndImprintContractResultDto((await GetContractAsync(contract.Id))!, 0);

        var works = await _db.Works.Where(w => w.AffiliateId == contract.AuthorId && w.ImprintId == imprintId).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var w in works)
        {
            w.ImprintId = null;
            w.UpdatedAt = now;
        }
        contract.Status = "ended";
        contract.EndedAt = now;
        contract.UpdatedAt = now;
        await _db.SaveChangesAsync(); // un solo SaveChanges: contrato y obras en la misma transacción
        return new EndImprintContractResultDto((await GetContractAsync(contract.Id))!, works.Count);
    }

    public async Task<IReadOnlyList<ImprintWorkDto>> GetWorksAsync(Guid imprintId)
    {
        await RequirePublisherAsync(imprintId);
        var works = await _db.Works.AsNoTracking()
            .Where(w => w.ImprintId == imprintId || w.AffiliateId == imprintId)
            .Select(w => new
            {
                w.Id, w.AffiliateId, AuthorName = w.Byline ?? w.Affiliate!.Name, AuthorSlug = w.Affiliate!.Slug,
                w.Title, w.Subtitle, w.Slug, w.Kind, w.Status, w.CoverUrl, w.CoverUrlEn, w.Year, w.Isbn,
                HasSynopsis = w.Synopsis != null && w.Synopsis != "", w.UpdatedAt, w.CreatedAt,
            })
            .OrderBy(w => w.AuthorName).ThenByDescending(w => w.Year).ThenBy(w => w.Title)
            .ToListAsync();
        var ids = works.Select(w => w.Id).ToList();
        var stats = await _db.WorkChapters.AsNoTracking()
            .Where(c => ids.Contains(c.WorkId))
            .GroupBy(c => c.WorkId)
            .Select(g => new { WorkId = g.Key, Count = g.Count(), Words = g.Sum(c => c.WordCount) })
            .ToDictionaryAsync(x => x.WorkId);
        var langs = await _db.WorkChapterTranslations.AsNoTracking()
            .Where(t => ids.Contains(t.WorkId) && t.Status == "published")
            .Select(t => new { t.WorkId, t.Language })
            .Distinct()
            .ToListAsync();

        return works.Select(w =>
        {
            stats.TryGetValue(w.Id, out var s);
            return new ImprintWorkDto(
                w.Id, w.AffiliateId, w.AuthorName, w.AuthorSlug, w.Title, w.Subtitle, w.Slug, w.Kind, w.Status,
                w.CoverUrl, w.CoverUrlEn, w.Year, w.Isbn, w.HasSynopsis, s?.Count ?? 0, s?.Words ?? 0,
                langs.Where(l => l.WorkId == w.Id).Select(l => l.Language).OrderBy(l => l).ToList(),
                w.UpdatedAt ?? w.CreatedAt);
        }).ToList();
    }

    // ── Lado del autor ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ImprintContractDto>> GetMyContractsAsync(Guid authorId)
    {
        var rows = await ContractsQuery().Where(c => c.AuthorId == authorId)
            .OrderBy(c => c.Status == "invited" ? 0 : c.Status == "active" ? 1 : 2).ThenBy(c => c.Imprint!.Name)
            .ToListAsync();
        return await ToDtosAsync(rows);
    }

    public async Task<ImprintContractDto?> RespondAsync(Guid authorId, Guid contractId, bool accept)
    {
        var contract = await _db.ImprintAuthors.FirstOrDefaultAsync(c => c.Id == contractId && c.AuthorId == authorId);
        if (contract is null) return null;
        if (contract.Status != "invited")
            throw new ArgumentException("Sello: esta invitación ya no está pendiente.");
        contract.Status = accept ? "active" : "declined";
        contract.RespondedAt = DateTime.UtcNow;
        contract.UpdatedAt = DateTime.UtcNow;
        if (accept && contract.StartsOn is null) contract.StartsOn = DateTime.UtcNow.Date;
        await _db.SaveChangesAsync();
        return await GetContractAsync(contract.Id);
    }

    public async Task<IReadOnlyList<ImprintOptionDto>> GetOptionsAsync(Guid affiliateId)
    {
        var options = await _db.ImprintAuthors.AsNoTracking()
            .Where(c => c.AuthorId == affiliateId && c.Status == "active")
            .Select(c => new ImprintOptionDto(c.ImprintId, c.Imprint!.Name, c.Imprint.Slug))
            .ToListAsync();
        // Un sello también publica obras propias.
        var self = await _db.Affiliates.AsNoTracking()
            .Where(a => a.Id == affiliateId && a.BusinessType == BusinessType.Publisher)
            .Select(a => new ImprintOptionDto(a.Id, a.Name, a.Slug))
            .FirstOrDefaultAsync();
        if (self is not null) options.Insert(0, self);
        return options;
    }

    public async Task<bool> CanUseImprintAsync(Guid affiliateId, Guid imprintId)
    {
        if (affiliateId == imprintId)
            return await _db.Affiliates.AnyAsync(a => a.Id == affiliateId && a.BusinessType == BusinessType.Publisher);
        return await _db.ImprintAuthors.AnyAsync(c => c.AuthorId == affiliateId && c.ImprintId == imprintId && c.Status == "active");
    }

    // ── Apoyo ────────────────────────────────────────────────────────────────────

    private async Task RequirePublisherAsync(Guid imprintId)
    {
        var ok = await _db.Affiliates.AnyAsync(a => a.Id == imprintId && a.BusinessType == BusinessType.Publisher);
        if (!ok) throw new InvalidOperationException("Este espacio no es un sello editorial.");
    }

    private IQueryable<ImprintAuthor> ContractsQuery()
        => _db.ImprintAuthors.AsNoTracking().Include(c => c.Imprint).Include(c => c.Author);

    private async Task<ImprintContractDto?> GetContractAsync(Guid contractId)
    {
        var row = await ContractsQuery().FirstOrDefaultAsync(c => c.Id == contractId);
        return row is null ? null : (await ToDtosAsync(new List<ImprintAuthor> { row }))[0];
    }

    private async Task<IReadOnlyList<ImprintContractDto>> ToDtosAsync(List<ImprintAuthor> rows)
    {
        if (rows.Count == 0) return Array.Empty<ImprintContractDto>();
        var pairs = rows.Select(r => new { r.ImprintId, r.AuthorId }).ToList();
        var authorIds = pairs.Select(p => p.AuthorId).Distinct().ToList();
        var imprintIds = pairs.Select(p => p.ImprintId).Distinct().ToList();
        var counts = await _db.Works.AsNoTracking()
            .Where(w => w.ImprintId != null && authorIds.Contains(w.AffiliateId) && imprintIds.Contains(w.ImprintId!.Value))
            .GroupBy(w => new { w.AffiliateId, ImprintId = w.ImprintId!.Value })
            .Select(g => new { g.Key.AffiliateId, g.Key.ImprintId, Count = g.Count() })
            .ToListAsync();

        return rows.Select(c => new ImprintContractDto(
            c.Id, c.ImprintId, c.Imprint?.Name ?? string.Empty, c.Imprint?.Slug, c.Imprint?.LogoUrl,
            c.AuthorId, c.Author?.Name ?? string.Empty, c.Author?.Slug,
            c.Status, c.RoyaltyEbookPercent, c.RoyaltyPrintPercent, SplitRights(c.Rights),
            c.StartsOn, c.EndsOn, c.Notes, c.CreatedAt, c.RespondedAt, c.EndedAt,
            counts.FirstOrDefault(x => x.AffiliateId == c.AuthorId && x.ImprintId == c.ImprintId)?.Count ?? 0)).ToList();
    }

    private static void ApplyTerms(ImprintAuthor c, decimal ebook, decimal print, IReadOnlyList<string>? rights,
        DateTime? startsOn, DateTime? endsOn, string? notes)
    {
        if (ebook is < 0 or > 100 || print is < 0 or > 100)
            throw new ArgumentException("Sello: las regalías van de 0 a 100 %.");
        var list = (rights ?? Array.Empty<string>())
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length > 0)
            .Distinct()
            .ToList();
        if (list.Count == 0) throw new ArgumentException("Sello: marca al menos un derecho (ebook, impreso…).");
        var bad = list.FirstOrDefault(x => !RightsAllowed.Contains(x));
        if (bad is not null) throw new ArgumentException($"Sello: derecho no válido: {bad}.");
        var start = ToUtcDate(startsOn);
        var end = ToUtcDate(endsOn);
        if (start is not null && end is not null && end <= start)
            throw new ArgumentException("Sello: el contrato debe terminar después de empezar.");

        c.RoyaltyEbookPercent = Math.Round(ebook, 2);
        c.RoyaltyPrintPercent = Math.Round(print, 2);
        c.Rights = string.Join(',', RightsAllowed.Where(list.Contains));
        c.StartsOn = start;
        c.EndsOn = end;
        var n = notes?.Trim();
        c.Notes = string.IsNullOrEmpty(n) ? null : n.Length > 2000 ? n[..2000] : n;
    }

    private static IReadOnlyList<string> SplitRights(string? rights)
        => (rights ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static DateTime? ToUtcDate(DateTime? d)
        => d is null ? null : DateTime.SpecifyKind(d.Value.Date, DateTimeKind.Utc);

    private static string NormalizeSlug(string? value)
    {
        var s = (value ?? string.Empty).Trim().ToLowerInvariant();
        // Acepta la dirección completa pegada: "https://maalca.com/ciriwhispers/" → "ciriwhispers".
        var slash = s.TrimEnd('/').LastIndexOf('/');
        if (slash >= 0) s = s.TrimEnd('/')[(slash + 1)..];
        return s.Trim('/', ' ');
    }
}
