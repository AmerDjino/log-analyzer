using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace LogAnalyzer.Api;

/// <summary>
/// Request bodies must be UTF-8, which RFC 8259 also requires for JSON. An endpoint that reads a body, meaning one with
/// <see cref="IAcceptsMetadata"/>, answers 415 UNSUPPORTED_CHARSET when the Content-Type names a charset other than
/// utf-8 or utf8 (in any case, quoted or not), before anything reads or binds the body. A missing charset means UTF-8.
/// Runs after authorization, so an anonymous request to a protected endpoint still gets 401 first.
/// </summary>
internal static class Utf8RequestBodies
{
    public static readonly ApiError UnsupportedCharset = new(
        "UNSUPPORTED_CHARSET",
        StatusCodes.Status415UnsupportedMediaType,
        "Unsupported character set",
        "Send the request body as UTF-8.");

    public static IApplicationBuilder UseUtf8RequestBodies(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<IAcceptsMetadata>() is not null
                && MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var mediaType)
                && mediaType.Charset.HasValue)
            {
                if (!IsUtf8(HeaderUtilities.RemoveQuotes(mediaType.Charset)))
                {
                    await UnsupportedCharset.ToResult().ExecuteAsync(context);
                    return;
                }

                // The JSON reader knows only the plain spelling: for "utf-8" in quotes, or utf8, it would answer 500.
                mediaType.Charset = "utf-8";
                context.Request.ContentType = mediaType.ToString();
            }

            await next(context);
        });

    private static bool IsUtf8(StringSegment charset) =>
        charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase)
        || charset.Equals("utf8", StringComparison.OrdinalIgnoreCase);
}
