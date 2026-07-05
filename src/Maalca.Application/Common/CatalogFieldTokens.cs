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
