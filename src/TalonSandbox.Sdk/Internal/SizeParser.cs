using System.Text.RegularExpressions;

namespace TalonSandbox.Internal;

/// <summary>
/// Parses human-readable size strings (e.g. "4GiB", "512MiB", "1.5GB") into bytes.
/// Supports IEC binary (KiB/MiB/GiB/TiB) and SI decimal (KB/MB/GB/TB) units.
/// Case-insensitive. Bare integers treated as bytes.
/// </summary>
internal static partial class SizeParser
{
    [GeneratedRegex(@"^\s*(\d+(?:\.\d+)?)\s*([a-zA-Z]*)\s*$")]
    private static partial Regex SizeRegex();

    private static readonly Dictionary<string, long> IecMultipliers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["b"]   = 1L,
        ["kib"] = 1024L,
        ["mib"] = 1024L * 1024,
        ["gib"] = 1024L * 1024 * 1024,
        ["tib"] = 1024L * 1024 * 1024 * 1024,
        ["kb"]  = 1_000L,
        ["mb"]  = 1_000_000L,
        ["gb"]  = 1_000_000_000L,
        ["tb"]  = 1_000_000_000_000L,
    };

    /// <summary>
    /// Parses <paramref name="input"/> into a byte count.
    /// </summary>
    /// <exception cref="FormatException">Thrown when the input cannot be parsed.</exception>
    public static long Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var m = SizeRegex().Match(input);
        if (!m.Success)
            throw new FormatException($"Cannot parse size '{input}'. Expected format: '4GiB', '512MiB', '1024'.");

        if (!double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Cannot parse numeric part of size '{input}'.");

        if (value < 0)
            throw new FormatException($"Size cannot be negative: '{input}'.");

        var unit = m.Groups[2].Value.Trim();

        if (string.IsNullOrEmpty(unit))
            return (long)value;

        if (!IecMultipliers.TryGetValue(unit, out var multiplier))
            throw new FormatException(
                $"Unknown size unit '{unit}' in '{input}'. Supported: B, KiB, MiB, GiB, TiB, KB, MB, GB, TB.");

        return (long)(value * multiplier);
    }
}
