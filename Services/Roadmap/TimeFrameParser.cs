namespace Xllerate.Services.Roadmap;

public static class TimeframeParser
{
    private static readonly Dictionary<string, int> NamedPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["week"] = 7,
        ["a week"] = 7,
        ["fortnight"] = 14,
        ["a fortnight"] = 14,
        ["month"] = 30,
        ["a month"] = 30,
        ["quarter"] = 90,
        ["a quarter"] = 90,
        ["year"] = 365,
        ["a year"] = 365,
    };

    // Returns (days, periodLabel) where periodLabel is "Day" | "Week" | "Month"
    public static (int Days, string PeriodLabel) Parse(string input)
    {
        input = input.Trim().ToLowerInvariant();

        if (NamedPresets.TryGetValue(input, out var preset))
            return (preset, PeriodLabelFor(preset));

        // Patterns: "2 weeks", "3 months", "10 days", "1.5 months", "half a month", "couple weeks"
        if (TryParseExpression(input, out int days))
            return (Math.Clamp(days, 1, 730), PeriodLabelFor(days));

        throw new ArgumentException($"Cannot parse timeframe: '{input}'");
    }

    private static bool TryParseExpression(string input, out int days)
    {
        days = 0;

        // "half a X"
        var halfMatch = System.Text.RegularExpressions.Regex.Match(input, @"half\s+a?\s*(day|week|month|year)");
        if (halfMatch.Success)
        {
            days = UnitToDays(halfMatch.Groups[1].Value) / 2;
            return true;
        }

        // "couple of X" / "couple X"
        var coupleMatch = System.Text.RegularExpressions.Regex.Match(input, @"couple\s+(?:of\s+)?(day|week|month|year)s?");
        if (coupleMatch.Success)
        {
            days = UnitToDays(coupleMatch.Groups[1].Value) * 2;
            return true;
        }

        // "N unit" where N is int or decimal
        var match = System.Text.RegularExpressions.Regex.Match(input,
            @"(\d+(?:\.\d+)?)\s*(day|week|month|year)s?");
        if (match.Success)
        {
            double amount = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            days = (int)Math.Round(amount * UnitToDays(match.Groups[2].Value));
            return true;
        }

        // plain integer — assume days
        if (int.TryParse(input, out int raw))
        {
            days = raw;
            return true;
        }

        return false;
    }

    private static int UnitToDays(string unit) => unit switch
    {
        "day"   => 1,
        "week"  => 7,
        "month" => 30,
        "year"  => 365,
        _       => 1
    };

    private static string PeriodLabelFor(int days) => days switch
    {
        <= 14  => "Day",
        <= 89  => "Week",
        _      => "Month"
    };
}