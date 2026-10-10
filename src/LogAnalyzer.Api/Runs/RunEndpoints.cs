using System.Buffers;
using System.Security.Claims;
using LogAnalyzer.Core.Ingestion;
using LogAnalyzer.Infrastructure.Runs;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Api.Runs;

/// <summary>The run endpoints under /runs (behind nginx: /api/runs). See docs/runs-api.md.</summary>
internal static class RunEndpoints
{
    // The body is read in pieces of this size, so a small paste never allocates the whole limit.
    private const int ReadChunkBytes = 81_920;

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

    // Reads at most limit + 1 bytes. A body above the limit is refused by its Content-Length before anything is read,
    // or by the extra byte arriving, which also covers a body sent without Content-Length. Null means too large.
    private static async Task<ReadOnlyMemory<byte>?> ReadBodyWithinLimitAsync(
        HttpRequest request, int limit, CancellationToken cancellationToken)
    {
        if (request.ContentLength > limit)
        {
            return null;
        }

        // Raised past this limit, so Kestrel's own (30,000,000 bytes by default, answered without a code) never answers first.
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } serverLimit)
        {
            serverLimit.MaxRequestBodySize = limit + 1L;
        }

        var content = new MemoryStream((int)(request.ContentLength ?? Math.Min(limit + 1L, ReadChunkBytes)));
        var chunk = ArrayPool<byte>.Shared.Rent(ReadChunkBytes);
        try
        {
            while (content.Length <= limit)
            {
                var wanted = (int)Math.Min(chunk.Length, limit + 1L - content.Length);
                var read = await request.Body.ReadAsync(chunk.AsMemory(0, wanted), cancellationToken);
                if (read == 0)
                {
                    return content.GetBuffer().AsMemory(0, (int)content.Length);
                }

                content.Write(chunk, 0, read);
            }

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
