using FluentAssertions;
using TalonSandbox.Internal;
using Xunit;

namespace TalonSandbox.Sdk.Tests;

public class SizeParserTests
{
    [Theory]
    [InlineData("1B", 1L)]
    [InlineData("1KiB", 1024L)]
    [InlineData("1MiB", 1048576L)]
    [InlineData("1GiB", 1073741824L)]
    [InlineData("4GiB", 4294967296L)]
    [InlineData("1TiB", 1099511627776L)]
    [InlineData("1KB", 1000L)]
    [InlineData("1MB", 1000000L)]
    [InlineData("1GB", 1000000000L)]
    [InlineData("512MiB", 536870912L)]
    [InlineData("1.5GiB", 1610612736L)]
    [InlineData("512", 512L)]
    [InlineData("  4GiB  ", 4294967296L)]   // whitespace trim
    [InlineData("4gib", 4294967296L)]        // case-insensitive
    public void Parse_ValidInput_ReturnsBytes(string input, long expected)
    {
        SizeParser.Parse(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("4XiB")]
    [InlineData("-1GiB")]
    public void Parse_InvalidInput_ThrowsFormatException(string input)
    {
        var act = () => SizeParser.Parse(input);
        act.Should().Throw<FormatException>();
    }
}
