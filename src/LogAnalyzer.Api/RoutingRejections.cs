namespace LogAnalyzer.Api;

/// <summary>
/// Routing answers a request it can match only by path, but not by HTTP method or content type, with a built-in
/// rejection endpoint (405 or 415). That endpoint carries no metadata, so the sign-in-by-default rule would turn
/// it into a 401 for anonymous callers, for example a form-encoded sign-in. Running it before authorization
/// gives every caller the real answer. Any endpoint the app maps has metadata and is not affected.
/// </summary>
internal static class RoutingRejections
{
    public static IApplicationBuilder UseRoutingRejectionsBeforeAuthorization(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var endpoint = context.GetEndpoint();
            if (endpoint is { RequestDelegate: { } rejection } and not RouteEndpoint && endpoint.Metadata.Count == 0)
            {
                await rejection(context);
                return;
            }

            await next(context);
        });
}
