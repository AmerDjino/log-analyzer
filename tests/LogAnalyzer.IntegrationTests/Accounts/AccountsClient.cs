using System.Net.Http.Json;

namespace LogAnalyzer.IntegrationTests.Accounts;

/// <summary>Calls the account endpoints. Each test makes its own email addresses, so tests sharing a database never collide.</summary>
public sealed class AccountsClient(HttpClient client)
{
    public const string Password = "correct horse battery";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public HttpClient Http => client;

    public Task<HttpResponseMessage> RegisterAsync(string email, string password, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync("/auth/register", new { email, password }, cancellationToken);

    public Task<HttpResponseMessage> SignInAsync(string email, string password, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync("/auth/sign-in", new { email, password }, cancellationToken);

    public Task<HttpResponseMessage> SignOutAsync(CancellationToken cancellationToken) =>
        client.PostAsync("/auth/sign-out", content: null, cancellationToken);

    public Task<HttpResponseMessage> MeAsync(CancellationToken cancellationToken) =>
        client.GetAsync("/auth/me", cancellationToken);

    /// <summary>Registers an account and checks that it worked.</summary>
    public async Task RegisterExistingAsync(string email, CancellationToken cancellationToken, string password = Password)
    {
        using var response = await RegisterAsync(email, password, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Registers an account and signs in with it, so this client holds a session cookie.</summary>
    public async Task<AccountResponse> RegisterAndSignInAsync(string email, CancellationToken cancellationToken)
    {
        await RegisterExistingAsync(email, cancellationToken);
        using var response = await SignInAsync(email, Password, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountResponse>(cancellationToken))!;
    }
}

public sealed record AccountResponse(Guid Id, string Email);
