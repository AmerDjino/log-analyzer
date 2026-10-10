using LogAnalyzer.Core.Ingestion;

namespace LogAnalyzer.UnitTests.Ingestion;

/// <summary>The level-format table from design.md: one row per recognized form, plus the lines that must not count.</summary>
public sealed class LineLevelsTests
{
    [Theory]
    // .NET console prefixes, in lowercase, followed by a colon.
    [InlineData("crit: Orders.Api[0] Process terminated", LineLevel.Critical)]
    [InlineData("fail: Orders.Api[0] Database connection pool exhausted", LineLevel.Error)]
    [InlineData("warn: Orders.Api[0] Connection pool at 90% of its limit", LineLevel.Warning)]
    [InlineData("info: Microsoft.Hosting.Lifetime[0] Application started.", LineLevel.Info)]
    [InlineData("dbug: Orders.Api[0] Cache miss", LineLevel.Debug)]
    [InlineData("trce: Orders.Api[0] Entering Handle", LineLevel.Trace)]
    // A level name as the last word inside square brackets, in any case (Serilog and others).
    [InlineData("[09:15:02 ERR] Payment failed", LineLevel.Error)]
    [InlineData("[ERR] Payment failed", LineLevel.Error)]
    [InlineData("[Error] Payment failed", LineLevel.Error)]
    [InlineData("[FTL] Host terminated", LineLevel.Critical)]
    [InlineData("[WRN] Slow response", LineLevel.Warning)]
    [InlineData("[INF] Started", LineLevel.Info)]
    [InlineData("[DBG] Cache hit", LineLevel.Debug)]
    [InlineData("[VRB] Entering", LineLevel.Trace)]
    [InlineData("2026-10-10 09:15:02.123 +02:00 [ERR] Database unavailable", LineLevel.Error)]
    // A level name in capitals as a whole word (log4j, logback, NLog, Python, Spring Boot).
    [InlineData("2026-10-10 09:15:02,123 ERROR [main] c.f.Bar - boom", LineLevel.Error)]
    [InlineData("2026-10-10 09:15:02.123+02:00  WARN 12345 --- [main] o.s.Foo : slow", LineLevel.Warning)]
    [InlineData("ERROR:root:Something failed", LineLevel.Error)]
    [InlineData("2026-10-10 09:15:02.1234|FATAL|Orders|crashed", LineLevel.Critical)]
    [InlineData("SEVERE: Connection refused", LineLevel.Error)]
    [InlineData("[main] INFO Starting service", LineLevel.Info)]
    // Every name of the table counts in capitals, short ones too (zerolog's console writer, among others).
    [InlineData("10:00AM INF Starting the server", LineLevel.Info)]
    [InlineData("2026-10-10 09:15:02 ERR Payment failed", LineLevel.Error)]
    [InlineData("WRN Slow response", LineLevel.Warning)]
    [InlineData("FTL Host terminated", LineLevel.Critical)]
    [InlineData("FAIL Checkout stopped", LineLevel.Error)]
    [InlineData("DBUG Cache miss", LineLevel.Debug)]
    [InlineData("DBG Cache hit", LineLevel.Debug)]
    [InlineData("TRCE Entering Handle", LineLevel.Trace)]
    [InlineData("TRC Entering Handle", LineLevel.Trace)]
    [InlineData("VRB Entering Handle", LineLevel.Trace)]
    // A short name must stand alone, but punctuation other than - . and / may touch it.
    [InlineData("npm ERR! code E404", LineLevel.Error)]
    [InlineData("--- FAIL: TestCheckout (0.01s)", LineLevel.Error)]
    [InlineData("[09:15:02.123|ERR] Payment failed", LineLevel.Error)]
    [InlineData("Oct 10 09:15:02 INF-WEB01 app[12]: ERROR payment failed", LineLevel.Error)]
    // Colour codes are not text: zerolog's and Serilog's coloured console output.
    [InlineData("\u001b[90m10:00AM\u001b[0m \u001b[32mINF\u001b[0m Starting the server", LineLevel.Info)]
    [InlineData("[09:15:02 \u001b[38;5;0197mERR\u001b[0m] Payment failed", LineLevel.Error)]
    [InlineData("\u001b[31mERROR\u001b[0m Payment failed", LineLevel.Error)]
    // Square brackets whose last word is not a level name are not a marker, so a marker inside them still counts.
    [InlineData("[ERROR main] Payment failed", LineLevel.Error)]
    [InlineData("[http-nio-8080-exec-1] WARN Slow response", LineLevel.Warning)]
    [InlineData("[Thread-1] [WRN] Slow response", LineLevel.Warning)]
    // A key followed by a level name, in any case (logfmt and JSON).
    [InlineData("time=2026-10-10T09:00:01Z level=error msg=\"boom\"", LineLevel.Error)]
    [InlineData("lvl=WARN msg=slow", LineLevel.Warning)]
    [InlineData("severity=critical component=db", LineLevel.Critical)]
    [InlineData("{\"@t\":\"2026-10-10T09:00:01Z\",\"@l\":\"Error\",\"@mt\":\"boom\"}", LineLevel.Error)]
    [InlineData("{\"level\":\"warn\",\"msg\":\"slow\"}", LineLevel.Warning)]
    [InlineData("{\"Timestamp\":\"2026-10-10T09:00:01Z\",\"Level\":\"Information\"}", LineLevel.Info)]
    [InlineData("{\"LogLevel\":\"Debug\",\"Message\":\"cache\"}", LineLevel.Debug)]
    // The leftmost marker wins.
    [InlineData("INFO Error rate is 0%", LineLevel.Info)]
    public void Detect_RecognizesEachForm(string line, LineLevel expected) =>
        Assert.Equal(expected, LineLevels.Detect(line));

    [Theory]
    [InlineData("   at Orders.Api.Handlers.CheckoutHandler.Handle(CheckoutCommand command)")]
    [InlineData("Retrying after error")]
    [InlineData("Error while saving the order")]
    [InlineData("ERR_CONNECTION_REFUSED while calling payments")]
    [InlineData("[main] starting")]
    [InlineData("Processed 0 errors and 2 warnings")]
    // Short names inside host names, paths, file names and identifiers.
    [InlineData("10.0.0.7 - - \"GET /reports/INF/2026 HTTP/1.1\" 200 512")]
    [InlineData("Ticket WRN-1234 reopened")]
    [InlineData("Loaded template welcome.FTL")]
    [InlineData("Score: -INF")]
    [InlineData("")]
    public void Detect_FindsNoLevelInMessagesAndContinuationLines(string line) =>
        Assert.Null(LineLevels.Detect(line));

    // A marker counts only when it starts within the first 100 characters.
    [Fact]
    public void Detect_IgnoresAMarkerBeyondTheFirst100Characters()
    {
        // ERROR starts at index 99, then at index 100.
        Assert.Equal(LineLevel.Error, LineLevels.Detect(new string('x', 98) + " ERROR boom"));
        Assert.Null(LineLevels.Detect(new string('x', 99) + " ERROR boom"));
    }
}
