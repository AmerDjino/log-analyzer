using LogAnalyzer.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // OpenAPI document at /openapi/v1.json; the Angular API client is generated from it.
    app.MapOpenApi();
}

// 200 "Healthy" when PostgreSQL answers, 503 "Unhealthy" otherwise.
// Used by the Docker Compose health check and, through nginx, as /api/health.
app.MapHealthChecks("/health");

app.Run();

// .NET 10 generates a public partial Program class for top-level statements,
// so the integration tests can host this API with WebApplicationFactory<Program>.
