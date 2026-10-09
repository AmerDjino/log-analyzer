using LogAnalyzer.Api;
using LogAnalyzer.Api.Accounts;
using LogAnalyzer.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure();
builder.Services.AddAccountSessions();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseRoutingRejectionsBeforeAuthorization();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    // OpenAPI document at /openapi/v1.json; the Angular API client is generated from it.
    app.MapOpenApi().AllowAnonymous();
}

// 200 "Healthy" when PostgreSQL answers, 503 "Unhealthy" otherwise.
// Used by the Docker Compose health check and, through nginx, as /api/health. Public, like registration and sign-in.
app.MapHealthChecks("/health").AllowAnonymous();

app.MapAccountEndpoints();

app.Run();

// .NET 10 generates a public partial Program class for top-level statements,
// so the integration tests can host this API with WebApplicationFactory<Program>.
