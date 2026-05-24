using FluentAssertions;
using TalonSandbox.Internal;
using Xunit;

namespace TalonSandbox.Sdk.Tests;

public class DurationParserTests
{
    [Theory]
    [InlineData("30s", 30L)]
    [InlineData("5m", 300L)]
    [InlineData("2h", 7200L)]
    [InlineData("1d", 86400L)]
    [InlineData("1w", 604800L)]
    [InlineData("100ms", 0L)]       // rounds down to 0 whole seconds
    [InlineData("1.5h", 5400L)]
    [InlineData("30", 30L)]         // bare integer = seconds
    [InlineData("  30m  ", 1800L)]  // whitespace
    [InlineData("0s", 0L)]
    [InlineData("2d", 172800L)]
    [InlineData("90s", 90L)]
    public void ParseSeconds_ValidInput_ReturnsSeconds(string input, long expectedSeconds)
    {
        DurationParser.ParseSeconds(input).Should().Be(expectedSeconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("5M")]   // months — ambiguous, not supported
    [InlineData("-1h")]
    public void ParseSeconds_InvalidInput_ThrowsFormatException(string input)
    {
        var act = () => DurationParser.ParseSeconds(input);
        act.Should().Throw<FormatException>();
    }
}
