using System.Text.Json;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Agents.Tools;

/// <summary>
/// Catálogo vivo del negocio (productos, precios, ofertas). El negocio sale del contexto del trabajo,
/// nunca de lo que escriba el modelo: un agente no puede pedir los datos de otro negocio.
/// </summary>
public class BusinessCatalogTool : IAgentTool
{
    private const int DefaultLimit = 30;
    private const int MaxLimit = 100;

    private readonly AppDbContext _db;

    public BusinessCatalogTool(AppDbContext db)
    {
        _db = db;
    }

    public string Name => "business_catalog";
    public string Description =>
        "Productos o platos publicados del negocio con su precio real, oferta (precio anterior), categoría y si son " +
        "destacados. Úsala siempre que vayas a mencionar un producto o un precio; nunca los inventes. " +
        "Puedes filtrar por texto y por categoría.";
    public string InputSchemaJson =>
        "{\"type\":\"object\",\"properties\":{" +
        "\"query\":{\"type\":\"string\",\"description\":\"Texto a buscar en nombre o descripción (opcional).\"}," +
        "\"category\":{\"type\":\"string\",\"description\":\"Categoría exacta (opcional).\"}," +
        "\"limit\":{\"type\":\"integer\",\"description\":\"Máximo de resultados, 1 a 100 (por defecto 30).\"}" +
        "},\"additionalProperties\":false}";
    public bool InternalOnly => false;

    public async Task<string> ExecuteAsync(AgentToolContext context, JsonElement input, CancellationToken ct)
    {
        if (context.AffiliateId is not Guid affiliateId)
            return "Esta herramienta necesita un negocio y esta conversación no tiene uno.";

        string? query = null, category = null;
        var limit = DefaultLimit;
        if (input.ValueKind == JsonValueKind.Object)
        {
            if (input.TryGetProperty("query", out var q) && q.ValueKind == JsonValueKind.String) query = q.GetString()?.Trim();
            if (input.TryGetProperty("category", out var c) && c.ValueKind == JsonValueKind.String) category = c.GetString()?.Trim();
            if (input.TryGetProperty("limit", out var l) && l.ValueKind == JsonValueKind.Number && l.TryGetInt32(out var n))
                limit = Math.Clamp(n, 1, MaxLimit);
        }

        var products = _db.Products.AsNoTracking()
            .Where(p => p.AffiliateId == affiliateId && p.Status == "Active" && p.IsPubliclyVisible);
        if (!string.IsNullOrEmpty(category)) products = products.Where(p => p.Category == category);
        if (!string.IsNullOrEmpty(query))
        {
            var like = $"%{query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            products = products.Where(p => EF.Functions.ILike(p.Name, like) || (p.Description != null && EF.Functions.ILike(p.Description, like)));
        }

        var rows = await products
            .OrderByDescending(p => p.Featured).ThenBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Take(limit)
            .Select(p => new { p.Name, p.Category, p.Price, p.CompareAtPrice, p.Description, p.Featured, p.Popular })
            .ToListAsync(ct);

        return rows.Count == 0
            ? "No hay productos publicados que coincidan."
            : JsonSerializer.Serialize(rows, AgentToolJson.Options);
    }
}
