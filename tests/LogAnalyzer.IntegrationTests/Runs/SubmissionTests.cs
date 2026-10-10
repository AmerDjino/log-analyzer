using System.Net;
using System.Text;
using LogAnalyzer.IntegrationTests.Accounts;

namespace LogAnalyzer.IntegrationTests.Runs;

public sealed class SubmissionTests(RunsApiFactory factory) : IClassFixture<RunsApiFactory>
{
    private const int OversizedByteCount = 1_572_864;

    [Fact]
    [Trait("Scenario", "log-ingestion/pasted-log-is-accepted")]
    public async Task Submit_AcceptsPastedLog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        string[] lines =
        [
            "info: Orders.Api[0] Application started",
            "fail: Orders.Api[0] Payment failed",
            "info: Orders.Api[0] Retrying payment",
        ];

        using var response = await runs.PasteAsync(string.Join('\n', lines), cancellationToken);

        var runId = await RunsClient.AcceptedRunIdAsync(response, cancellationToken);
        var run = await database.ReadRunAsync(runId, cancellationToken);
        Assert.Equal(person.Id, run.UserId);
        Assert.Null(run.FileName);
        Assert.Equal("queued", run.Status);
        Assert.Equal(3, run.LineCount);
        Assert.Equal(lines, await database.ReadLinesAsync(runId, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "log-ingestion/uploaded-log-file-is-accepted")]
    public async Task Submit_AcceptsUploadedFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var content = Encoding.UTF8.GetBytes("2026-10-10 09:00:01.000 +00:00 [ERR] Zahlung für Bestellung 42 fehlgeschlagen\n");

        using var response = await runs.UploadAsync("orders-api.log", content, cancellationToken);

        var runId = await RunsClient.AcceptedRunIdAsync(response, cancellationToken);
        var run = await database.ReadRunAsync(runId, cancellationToken);
        Assert.Equal(person.Id, run.UserId);
        Assert.Equal("orders-api.log", run.FileName);
        Assert.Equal(content.Length, run.ByteCount);
    }

    [Fact]
    [Trait("Scenario", "log-ingestion/binary-file-is-rejected")]
    public async Task Submit_RejectsBinaryFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        // Valid UTF-8 apart from the NUL bytes, so only the NUL rule can reject it.
        byte[] content = [.. "header\n"u8, 0x00, 0x01, 0x02, 0x00, .. "\ntrailer\n"u8];

        using var response = await runs.UploadAsync("orders-api.dat", content, cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "UNSUPPORTED_CONTENT", cancellationToken);
        Assert.Equal(0, await database.CountRunsAsync(person.Id, cancellationToken));
    }

    [Fact]
    public async Task Submit_RejectsInvalidUtf8()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        // "Café" in Latin-1: the lone 0xE9 byte is not UTF-8.
        byte[] content = [.. "Caf"u8, 0xE9, (byte)'\n'];

        using var response = await runs.UploadAsync("latin1.log", content, cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "UNSUPPORTED_CONTENT", cancellationToken);
        Assert.Equal(0, await database.CountRunsAsync(person.Id, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "log-ingestion/unsupported-character-set-is-rejected")]
    public async Task Submit_RejectsUnsupportedCharset()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);

        using var response = await runs.SubmitAsync(
            RunsClient.Body("info: started\n"u8.ToArray(), "text/plain; charset=bogus"), fileName: null, cancellationToken);

        await ProblemAssert.HasCodeAsync(
            response, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_CHARSET", cancellationToken);
        Assert.Equal(0, await database.CountRunsAsync(person.Id, cancellationToken));
    }

    // A missing charset means UTF-8, and the name is matched in any case, quoted or not.
    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/plain; charset=UTF-8")]
    [InlineData("text/plain; charset=\"utf-8\"")]
    [InlineData("text/plain;charset=utf8")]
    public async Task Submit_AcceptsUtf8InEverySpelling(string contentType)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);

        using var response = await runs.SubmitAsync(
            RunsClient.Body("info: started\n"u8.ToArray(), contentType), fileName: null, cancellationToken);

        await RunsClient.AcceptedRunIdAsync(response, cancellationToken);
    }

    [Fact]
    [Trait("Scenario", "log-ingestion/line-breaks-do-not-add-lines")]
    public async Task Submit_CountsCrlfLinesWithoutAddingOne()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);

        using var response = await runs.PasteAsync("first line\r\nsecond line\r\nthird line\r\n", cancellationToken);

        var runId = await RunsClient.AcceptedRunIdAsync(response, cancellationToken);
        Assert.Equal(3, (await database.ReadRunAsync(runId, cancellationToken)).LineCount);
        Assert.Equal(
            new[] { "first line", "second line", "third line" },
            await database.ReadLinesAsync(runId, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "log-ingestion/whitespace-only-paste-is-rejected")]
    public async Task Submit_RejectsWhitespaceOnlyPaste()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);

        using var response = await runs.PasteAsync("  \t\n \r\n\t  \n", cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "EMPTY_INPUT", cancellationToken);
        Assert.Equal(0, await database.CountRunsAsync(person.Id, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "log-ingestion/empty-file-is-rejected")]
    public async Task Submit_RejectsEmptyFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);

        using var response = await runs.UploadAsync("empty.log", [], cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "EMPTY_INPUT", cancellationToken);
        Assert.Equal(0, await database.CountRunsAsync(person.Id, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "log-ingestion/oversized-upload-is-rejected")]
    public async Task Submit_RejectsOversizedUpload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);

        using var response = await runs.UploadAsync("large.log", LogOfSize(OversizedByteCount), cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.RequestEntityTooLarge, "INPUT_TOO_LARGE", cancellationToken);
        Assert.Equal(0, await database.CountRunsAsync(person.Id, cancellationToken));
    }

    // Without Content-Length the limit can only be found while reading, as for a chunked upload.
    [Fact]
    public async Task Submit_RejectsOversizedBodyWithoutContentLength()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        using var body = new UnknownLengthContent(LogOfSize(OversizedByteCount));
        Assert.Null(body.Headers.ContentLength);

        using var response = await runs.SubmitAsync(body, "large.log", cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.RequestEntityTooLarge, "INPUT_TOO_LARGE", cancellationToken);
        Assert.Equal(0, await database.CountRunsAsync(person.Id, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "log-ingestion/upload-at-the-limit-is-accepted")]
    public async Task Submit_AcceptsUploadAtTheLimit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);

        using var response = await runs.UploadAsync("limit.log", LogOfSize(RunsApiFactory.MaxSubmissionBytes), cancellationToken);

        var runId = await RunsClient.AcceptedRunIdAsync(response, cancellationToken);
        Assert.Equal(RunsApiFactory.MaxSubmissionBytes, (await database.ReadRunAsync(runId, cancellationToken)).ByteCount);
    }

    // Each submission gets one answer, checked in design.md's order: the session, the charset, the size, then the content.
    [Fact]
    public async Task Submit_ChecksSessionCharsetSizeAndContentInOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var anonymous = new RunsClient(factory.CreateClientWithoutCookies());
        var oversizedBinary = new byte[OversizedByteCount];

        using var withoutSession = await anonymous.SubmitAsync(
            RunsClient.Body(oversizedBinary, "text/plain; charset=bogus"), fileName: null, cancellationToken);
        using var withBogusCharset = await runs.SubmitAsync(
            RunsClient.Body(oversizedBinary, "text/plain; charset=bogus"), fileName: null, cancellationToken);
        using var tooLarge = await runs.UploadAsync("large.bin", oversizedBinary, cancellationToken);

        await ProblemAssert.HasCodeAsync(withoutSession, HttpStatusCode.Unauthorized, "SIGN_IN_REQUIRED", cancellationToken);
        await ProblemAssert.HasCodeAsync(
            withBogusCharset, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_CHARSET", cancellationToken);
        await ProblemAssert.HasCodeAsync(tooLarge, HttpStatusCode.RequestEntityTooLarge, "INPUT_TOO_LARGE", cancellationToken);
    }

    // A UTF-8 log of exactly the given size: lines of 99 characters and a line break, the last line cut short.
    private static byte[] LogOfSize(int byteCount) =>
        Enumerable.Range(0, byteCount).Select(index => index % 100 == 99 ? (byte)'\n' : (byte)'x').ToArray();

    // A body whose length is not known in advance, so the client sends it without Content-Length.
    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _content;

        public UnknownLengthContent(byte[] content)
        {
            _content = content;
            Headers.ContentType = new("text/plain");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_content).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
