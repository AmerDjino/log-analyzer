using System.Text.Json;
using System.Text.Json.Serialization;
using LogAnalyzer.Api;
using LogAnalyzer.Api.Accounts;
using LogAnalyzer.Api.Runs;
using LogAnalyzer.Core.Ingestion;
using LogAnalyzer.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
// An unreadable request body (empty or malformed JSON) answers 400. Left at its Development default, minimal APIs
// throw instead, and the exception handler turns that into a 500.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
// Statuses, severities and categories are written in lowercase, as stored and as the output contract spells them.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure();
builder.Services.AddAccountSessions();
// The submission size limit, checked when the API starts.
builder.Services.AddOptions<IngestionOptions>()
    .BindConfiguration(IngestionOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseRoutingRejectionsBeforeAuthorization();
app.UseAuthorization();
app.UseUtf8RequestBodies();

if (app.Environment.IsDevelopment())
{
    // OpenAPI document at /openapi/v1.json; the Angular API client is generated from it.
    app.MapOpenApi().AllowAnonymous();
}

// 200 "Healthy" when PostgreSQL answers, 503 "Unhealthy" otherwise.
// Used by the Docker Compose health check and, through nginx, as /api/health. Public, like registration and sign-in.
app.MapHealthChecks("/health").AllowAnonymous();

app.MapAccountEndpoints();
app.MapRunEndpoints();

app.Run();

// .NET 10 generates a public partial Program class for top-level statements,
// so the integration tests can host this API with WebApplicationFactory<Program>.
