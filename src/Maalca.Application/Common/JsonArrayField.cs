using System.Text.Json;

namespace Maalca.Application.Common;

// Generic parse helper for JSON-array-of-objects columns (Affiliate.ProcessSteps, Affiliate.Faq)
// — same "raw string column, typed DTO at the edges" convention as ModuleCatalog/TokenList.
public static class JsonArrayField
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static List<T> Parse<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<T>();

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>();
        }
        catch (JsonException)
        {
            return new List<T>();
        }
    }

    public static string Serialize<T>(IEnumerable<T> items) => JsonSerializer.Serialize(items, Options);
}
