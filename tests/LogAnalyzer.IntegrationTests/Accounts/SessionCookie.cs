namespace LogAnalyzer.IntegrationTests.Accounts;

/// <summary>Reads the session cookie from responses and sends it by hand.</summary>
public static class SessionCookie
{
    public const string Name = "la_session";

    /// <summary>The full Set-Cookie header for the session cookie, or null when the response sets none.</summary>
    public static string? FindSetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(value => value.StartsWith(Name + "=", StringComparison.Ordinal))
            : null;

    /// <summary>The session cookie a response must have set; fails the test otherwise.</summary>
    public static string RequireValue(HttpResponseMessage response) =>
        ValueOf(FindSetCookie(response) ?? throw new InvalidOperationException("The response set no session cookie."));

    /// <summary>The cookie's value from a Set-Cookie header.</summary>
    public static string ValueOf(string setCookie) =>
        setCookie[(Name.Length + 1)..].Split(';')[0];

    /// <summary>The cookie's attributes, lowercased, for example "httponly" or "samesite=strict".</summary>
    public static IReadOnlyList<string> AttributesOf(string setCookie) =>
        setCookie.Split(';').Skip(1).Select(part => part.Trim().ToLowerInvariant()).ToList();

    /// <summary>The same value with one character changed.</summary>
    public static string Tamper(string value)
    {
        var index = value.Length / 2;
        var replacement = value[index] == 'A' ? 'B' : 'A';
        return string.Concat(value.AsSpan(0, index), replacement.ToString(), value.AsSpan(index + 1));
    }

    /// <summary>A request that carries the given session cookie value.</summary>
    public static HttpRequestMessage Request(HttpMethod method, string path, string cookieValue)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", $"{Name}={cookieValue}");
        return request;
    }
}
