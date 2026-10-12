using System.Buffers;
using System.Globalization;
using System.Security.Claims;
using LogAnalyzer.Core.Ingestion;
using LogAnalyzer.Infrastructure.Runs;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Api.Runs;

/// <summary>The run endpoints under /runs (behind nginx: /api/runs). See docs/runs-api.md.</summary>
internal static class RunEndpoints
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    // The body is read in pieces of this size, so a small paste never allocates the whole limit.
    private const int ReadChunkBytes = 81_920;

    // Room for a chunked body's framing in Kestrel's own limit: a few bytes per chunk, little for usual chunk sizes.
    private const int FramingAllowanceBytes = 65_536;

    public static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var runs = app.MapGroup("/runs").WithTags("Runs");

        // The body is the log's bytes. Declaring text/plain makes routing answer 415 for any other media type, and
        // the OpenAPI document describes the body as binary, so a generated client sends a file's bytes unchanged.
        runs.MapPost(string.Empty, SubmitAsync)
            .Accepts<Stream>("text/plain")
            .Produces<SubmissionResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        runs.MapGet(string.Empty, ListAsync)
            .Produces<RunPageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        runs.MapGet("/{id}", ReadAsync)
            .Produces<RunResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
        runs.MapGet("/{id}/lines", ReadLinesAsync)
            .Produces<RunLinesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
        // A literal segment takes precedence over {id}, so "limits" is never read as a run id.
        runs.MapGet("/limits", ReadLimits)
            .Produces<SubmissionLimitsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    // The session, the media type and the charset are checked before this handler runs. Its own checks follow in a
    // fixed order, so each submission gets one answer: the size, then the bytes, then emptiness.
    private static async Task<IResult> SubmitAsync(
        HttpRequest request,
        string? fileName,
        ClaimsPrincipal user,
        RunStore runs,
        IOptions<IngestionOptions> options,
        CancellationToken cancellationToken)
    {
        var limit = options.Value.MaxSubmissionBytes;
        if (await ReadBodyWithinLimitAsync(request, limit, cancellationToken) is not { } content)
        {
            return RunErrors.InputTooLarge(limit).ToResult();
        }

        var log = LogText.Read(content.Span);
        switch (log.Problem)
        {
            case LogTextProblem.UnsupportedContent:
                return RunErrors.UnsupportedContent.ToResult();
            case LogTextProblem.Empty:
                return RunErrors.EmptyInput.ToResult();
        }

        var runId = await runs.SubmitAsync(
            UserIdOf(user), FileNames.Clean(fileName), content.Length, log.Lines, LogStatistics.Compute(log.Lines), cancellationToken);

        // No Location header: behind nginx, /runs/{id} would point at the web app.
        return TypedResults.Accepted((string?)null, new SubmissionResponse(runId, RunStatus.Queued));
    }

    // The id is read as text, so a malformed id gets the same answer as an unknown one or another person's run,
    // rather than routing's 404 without a code.
    private static async Task<IResult> ReadAsync(string id, ClaimsPrincipal user, RunStore runs, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var runId) || await runs.FindOwnedAsync(UserIdOf(user), runId, cancellationToken) is not { } run)
        {
            return RunErrors.RunNotFound.ToResult();
        }

        return TypedResults.Ok(RunResponse.From(run));
    }

    // The id is read as text, as in ReadAsync. from and to are bound as whole numbers: the spec names no error for a value
    // the API cannot read, so a missing or unreadable one gets the framework's 400 without a code.
    private static async Task<IResult> ReadLinesAsync(
        string id, int from, int to, ClaimsPrincipal user, RunStore runs, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var runId)
            || await runs.ReadOwnedLinesAsync(UserIdOf(user), runId, from, to, cancellationToken) is not { } lines)
        {
            return RunErrors.RunNotFound.ToResult();
        }

        return TypedResults.Ok(RunLinesResponse.From(from, lines));
    }

    // limit and cursor are read as text, so every value the API cannot use gets a coded answer rather than the binder's 400.
    private static async Task<IResult> ListAsync(
        string? limit, string? cursor, ClaimsPrincipal user, RunStore runs, CancellationToken cancellationToken)
    {
        var pageSize = DefaultPageSize;
        if (limit is not null
            && !(int.TryParse(limit, NumberStyles.None, CultureInfo.InvariantCulture, out pageSize) && pageSize is >= 1 and <= MaxPageSize))
        {
            return RunErrors.InvalidLimit.ToResult();
        }

        RunPosition? after = null;
        if (cursor is not null)
        {
            if (!RunCursor.TryDecode(cursor, out var position))
            {
                return RunErrors.InvalidCursor.ToResult();
            }

            after = position;
        }

        // One run more than the page holds shows whether another page follows.
        var runsAfter = await runs.ListOwnedAsync(UserIdOf(user), after, pageSize + 1, cancellationToken);
        var page = runsAfter.Take(pageSize).ToList();
        var nextCursor = runsAfter.Count > pageSize ? RunCursor.Encode(page[^1]) : null;
        return TypedResults.Ok(new RunPageResponse(page.Select(RunListItemResponse.From).ToList(), nextCursor));
    }

    // The limit SubmitAsync enforces, so a client can refuse a larger file before sending it.
    private static IResult ReadLimits(IOptions<IngestionOptions> options) =>
        TypedResults.Ok(new SubmissionLimitsResponse(options.Value.MaxSubmissionBytes));

    // Reads at most limit + 1 bytes of the log. A body above the limit is refused by its Content-Length before anything is
    // read, or by the byte past the limit arriving, which also covers a body sent without Content-Length. Null means too
    // large.
    private static async Task<ReadOnlyMemory<byte>?> ReadBodyWithinLimitAsync(
        HttpRequest request, int limit, CancellationToken cancellationToken)
    {
        if (request.ContentLength > limit)
        {
            return null;
        }

        // Kestrel counts a chunked body's framing toward its own limit, so the limit gets room for framing: only a body
        // whose framing is out of proportion reaches it, and that refusal is answered below like any body over the limit.
        // Kestrel's default, 30,000,000 bytes, would otherwise answer first when the limit is set higher.
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } serverLimit)
        {
            serverLimit.MaxRequestBodySize = 2L * limit + FramingAllowanceBytes;
        }

        // Grown as bytes arrive, never past the declared length or the limit, so a Content-Length the client may never
        // send allocates nothing up front.
        var capacityLimit = (int)(request.ContentLength ?? limit);
        var content = new MemoryStream(Math.Min(capacityLimit, ReadChunkBytes));
        var chunk = ArrayPool<byte>.Shared.Rent(ReadChunkBytes);
        try
        {
            while (true)
            {
                var wanted = (int)Math.Min(chunk.Length, limit + 1L - content.Length);
                var read = await request.Body.ReadAsync(chunk.AsMemory(0, wanted), cancellationToken);
                if (read == 0)
                {
                    return content.GetBuffer().AsMemory(0, (int)content.Length);
                }

                if (content.Length + read > limit)
                {
                    return null;
                }

                if (content.Length + read > content.Capacity)
                {
                    content.Capacity = (int)Math.Min(Math.Max(content.Length + read, 2L * content.Capacity), capacityLimit);
                }

                content.Write(chunk, 0, read);
            }
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }

    // The signed-in person's account id. Every run query filters on it.
    private static Guid UserIdOf(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The session has no account id."));
}
