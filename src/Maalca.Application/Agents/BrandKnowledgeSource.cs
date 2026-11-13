using System.Text;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Agents;

/// <summary>
/// Conocimiento de marca de un negocio: quién es, cómo habla y qué importa. El runner lo agrega al
/// prompt del agente cuando el trabajo trae un negocio. Aquí NO van precios ni menú: eso lo pide el
/// agente con herramientas (business_catalog) para que el dato siempre salga de la base.
/// </summary>
public class BrandKnowledgeSource : IKnowledgeSource
{
    private readonly AppDbContext _db;

    public BrandKnowledgeSource(AppDbContext db)
    {
        _db = db;
    }

    public string Name => "brand";

    public async Task<string?> GetContextAsync(Guid affiliateId, string query, CancellationToken ct)
    {
        var affiliate = await _db.Affiliates.AsNoTracking()
            .Where(a => a.Id == affiliateId)
            .Select(a => new { a.Name, a.Description, a.Horario })
            .FirstOrDefaultAsync(ct);
        if (affiliate is null) return null;

        var brand = await _db.BrandProfiles.AsNoTracking().FirstOrDefaultAsync(b => b.AffiliateId == affiliateId, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"Negocio: {affiliate.Name}");
        Line(sb, "Descripción pública", affiliate.Description);
        Line(sb, "Horario (JSON)", affiliate.Horario);
        if (brand is null)
        {
            sb.AppendLine("Perfil de marca: todavía no está llenado. No inventes tono ni datos de la marca; pídele al equipo de MaalCa que lo complete.");
        }
        else
        {
            Line(sb, "Voz de la marca", brand.Voice);
            Line(sb, "Público", brand.Audience);
            Line(sb, "Qué ofrece", brand.Offer);
            Line(sb, "Qué sí hacer", brand.Dos);
            Line(sb, "Qué nunca decir o mostrar", brand.Donts);
            Line(sb, "Fechas y temporadas importantes", brand.SpecialDates);
            Line(sb, "Hashtags", brand.Hashtags);
            Line(sb, "Idioma de las publicaciones", brand.Language);
            Line(sb, "Notas", brand.Notes);
        }
        return sb.ToString().TrimEnd();
    }

    private static void Line(StringBuilder sb, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) sb.AppendLine($"{label}: {value.Trim()}");
    }
}
