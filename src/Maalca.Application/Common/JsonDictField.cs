using System.Text.Json;

namespace Maalca.Application.Common;

// Same "raw string column, typed at the edges" convention as JsonArrayField, but for
// dictionary-shaped columns (Affiliate.SectionVisibility: { "processSteps": true, ... }).
public static class JsonDictField
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static Dictionary<string, bool> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, bool>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, bool>>(json, Options) ?? new Dictionary<string, bool>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, bool>();
        }
    }

    public static string Serialize(IReadOnlyDictionary<string, bool> dict) => JsonSerializer.Serialize(dict, Options);
}
