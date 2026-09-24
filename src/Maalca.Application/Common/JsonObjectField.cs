using System.Text.Json;

namespace Maalca.Application.Common;

// Same "raw string column, typed at the edges" convention as JsonArrayField/JsonDictField, but
// for a column that holds a single object (Affiliate.CommunityImpact) instead of an array or a
// string->bool dictionary.
public static class JsonObjectField
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static T? Parse<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
