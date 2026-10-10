using LogAnalyzer.Core.Ingestion;

namespace LogAnalyzer.UnitTests.Ingestion;

public sealed class FileNamesTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("orders-api.log", "orders-api.log")]
    [InlineData("C:\\logs\\orders-api.log", "orders-api.log")]
    [InlineData("/var/log/orders-api.log", "orders-api.log")]
    [InlineData("logs/", null)]
    [InlineData("  orders.log  ", "orders.log")]
    [InlineData("ord\u0000ers\u001B.log", "orders.log")]
    public void Clean_KeepsTheLastPathSegmentWithoutControlCharacters(string? fileName, string? expected) =>
        Assert.Equal(expected, FileNames.Clean(fileName));

    [Fact]
    public void Clean_CutsTo255CharactersWithoutSplittingOne()
    {
        var cleaned = FileNames.Clean(string.Concat(Enumerable.Repeat("\U0001F600", 300)));

        Assert.Equal(255, cleaned!.EnumerateRunes().Count());
        Assert.True(cleaned.EnumerateRunes().All(rune => rune.Value == 0x1F600));
    }
}
