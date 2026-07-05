namespace Maalca.Application.Common;

// Shared comma-separated-string <-> list conversion, same convention as
// Affiliate.ModulosActivos (ModuleCatalog.FilterActive) — used for Product.Periods/
// WeekDays/Flags so those columns don't need a separate array/JSON column type.
public static class TokenList
{
    public static List<string> Parse(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new List<string>();

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    public static string? Join(IEnumerable<string>? tokens)
    {
        var list = tokens?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        return list is null || list.Count == 0 ? null : string.Join(',', list);
    }
}
