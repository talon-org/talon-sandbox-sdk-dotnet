using System.Text.RegularExpressions;

namespace TalonSandbox.Internal;

/// <summary>
/// Parses human-readable duration strings into seconds.
/// Supports: ms, s, m, h, d, w. Bare integers are treated as seconds.
/// Case-sensitive for units (lowercase required). Negative values rejected.
/// </summary>
internal static partial class DurationParser
{
    [GeneratedRegex(@"^\s*(\d+(?:\.\d+)?)\s*([a-z]*)\s*$")]
    private static partial Regex DurationRegex();

    // multipliers → seconds
    private static readonly Dictionary<string, double> Multipliers = new()
    {
        ["ms"] = 0.001,
        ["s"]  = 1.0,
        ["m"]  = 60.0,
        ["h"]  = 3600.0,
        ["d"]  = 86400.0,
        ["w"]  = 604800.0,
    };

    /// <summary>
    /// Parses <paramref name="input"/> and returns the duration in whole seconds (truncated).
    /// </summary>
    /// <exception cref="FormatException">Thrown for invalid or unsupported input.</exception>
    public static long ParseSeconds(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var m = DurationRegex().Match(input);
        if (!m.Success)
            throw new FormatException(
                $"Cannot parse duration '{input}'. Expected: '30s', '5m', '2h', '1d', '1w', or bare integer seconds.");

        if (!double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Cannot parse numeric part of duration '{input}'.");

        var unit = m.Groups[2].Value;

        if (string.IsNullOrEmpty(unit))
            return (long)value;

        if (!Multipliers.TryGetValue(unit, out var multiplier))
            throw new FormatException(
                $"Unknown duration unit '{unit}' in '{input}'. Supported: ms, s, m, h, d, w.");

        return (long)(value * multiplier);
    }
}
