using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace FrotaOnline.Api.OpenApi;

/// <summary>Describes the API and declares the JWT bearer scheme so docs tools can offer an "Authorize" action.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeId = "Bearer";

    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "Frota Online API";
        document.Info.Version = "v1";
        document.Info.Description = "Freight marketplace for cargo-vehicle drivers.";

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token returned by /api/v1/auth/login (without the 'Bearer ' prefix)."
        };

        return Task.CompletedTask;
    }
}
