namespace Maalca.Application.Common;

// Whitelists for Product.Periods/WeekDays — validated on write so garbage tokens
// 400 instead of silently persisting. Flags is intentionally NOT whitelisted here
// (product decision: free-form dietary tags, e.g. vegetarian/spicy/glutenFree today,
// more later without a migration).
public static class MealPeriodTokens
{
    public static readonly HashSet<string> Whitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "breakfast", "lunch", "dinner", "late_night", "all_day"
    };
}

public static class WeekDayTokens
{
    public static readonly HashSet<string> Whitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"
    };
}

// Whitelist for Affiliate.Horario entries — Spanish day names (no accents), matching the
// "dia"/"abre"/"cierra"/"cerrado" field naming already used for that column.
public static class DiaSemanaTokens
{
    public static readonly HashSet<string> Whitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "lunes", "martes", "miercoles", "jueves", "viernes", "sabado", "domingo"
    };
}
