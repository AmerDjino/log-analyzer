using System.Text;
using LogAnalyzer.Core.Ingestion;

namespace LogAnalyzer.UnitTests.Ingestion;

/// <summary>Reading a submission's bytes: the content checks in design.md's order, then line splitting.</summary>
public sealed class LogTextTests
{
    [Theory]
    [InlineData("a\r\nb\r\nc\r\n", new[] { "a", "b", "c" })]
    [InlineData("a\nb\nc", new[] { "a", "b", "c" })]
    [InlineData("a\rb\rc\r", new[] { "a", "b", "c" })]
    [InlineData("a\r\n\r\nb", new[] { "a", "", "b" })]
    [InlineData("\na", new[] { "", "a" })]
    [InlineData("a\n\n", new[] { "a", "" })]
    [InlineData("  indented \t", new[] { "  indented \t" })]
    public void Read_SplitsAtCrlfLfAndCrWithoutAddingALineForTheFinalBreak(string content, string[] expected)
    {
        var result = LogText.Read(Encoding.UTF8.GetBytes(content));

        Assert.Null(result.Problem);
        Assert.Equal(expected, result.Lines);
    }

    [Fact]
    public void Read_KeepsUnicodeLineSeparatorsInsideALine()
    {
        var result = LogText.Read(Encoding.UTF8.GetBytes($"a{(char)0x2028}b{(char)0x85}c"));

        Assert.Equal(new[] { $"a{(char)0x2028}b{(char)0x85}c" }, result.Lines);
    }

    [Fact]
    public void Read_RemovesOneLeadingByteOrderMark()
    {
        var result = LogText.Read([0xEF, 0xBB, 0xBF, (byte)'a', (byte)'\n', (byte)'b']);

        Assert.Equal(new[] { "a", "b" }, result.Lines);
    }

    [Theory]
    [InlineData(new byte[] { (byte)'a', 0x00, (byte)'b' })]
    [InlineData(new byte[] { 0xC3, 0x28 })]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })]
    [InlineData(new byte[] { 0xC0, 0xAF })]
    [InlineData(new byte[] { (byte)'a', 0xFF })]
    public void Read_RejectsNulAndInvalidUtf8AsUnsupportedContent(byte[] content)
    {
        var result = LogText.Read(content);

        Assert.Equal(LogTextProblem.UnsupportedContent, result.Problem);
        Assert.Empty(result.Lines);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { (byte)' ', (byte)'\t', (byte)'\r', (byte)'\n' })]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF })]
    [InlineData(new byte[] { 0xC2, 0xA0, (byte)'\n' })]
    public void Read_RejectsContentWithNothingButWhiteSpaceAsEmpty(byte[] content)
    {
        var result = LogText.Read(content);

        Assert.Equal(LogTextProblem.Empty, result.Problem);
        Assert.Empty(result.Lines);
    }

    // The content check comes first, so white space around a NUL is still unsupported content.
    [Fact]
    public void Read_ChecksContentBeforeEmptiness() =>
        Assert.Equal(LogTextProblem.UnsupportedContent, LogText.Read([(byte)' ', 0x00, (byte)'\n']).Problem);
}
