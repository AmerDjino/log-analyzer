using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LogAnalyzer.Api;

/// <summary>
/// Adds <c>code</c> to the <see cref="ProblemDetails"/> schema of the OpenAPI document, as a string whose values are
/// <see cref="ApiErrorCodes.All"/>. Generated clients get a closed list of codes to match on.
/// </summary>
internal sealed class ProblemCodeSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(ProblemDetails))
        {
            schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            schema.Properties["code"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "What went wrong, for clients to match on. Answers that the framework produces, "
                    + "such as a 400 for a malformed JSON body, carry no code.",
                Enum = [.. ApiErrorCodes.All.Select(code => JsonValue.Create(code))],
            };
        }

        return Task.CompletedTask;
    }
}
