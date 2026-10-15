using System.Text.Json.Serialization;
using FrotaOnline.Api.Extensions;
using FrotaOnline.Api.OpenApi;
using FrotaOnline.Application;
using FrotaOnline.Infrastructure;
using FrotaOnline.Infrastructure.Seeding;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication();
builder.Services.AddInternalJobsAuthentication(builder.Configuration);

builder.Services
    .AddControllers(options => PortugueseModelBindingMessages.Apply(options.ModelBindingMessageProvider))
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Behind Cloud Run / a load balancer the app sees plain HTTP: trust X-Forwarded-Proto (so HTTPS redirection does not
// loop) and X-Forwarded-For (so the consent IP is the client's). Safe here because the app is only reachable through it.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// CORS for browser-based front-ends (the Ionic web view, ionic serve, the web app). Native HTTP stacks ignore it.
// Origins come from Cors:AllowedOrigins (Cors__AllowedOrigins__0, __1, ...); with none configured no origin is allowed.
// Authentication is a Bearer header, never a cookie, so credentials stay off and a wildcard would still be a bad idea.
const string AppCorsPolicy = "App";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddPolicy(AppCorsPolicy, policy =>
{
    if (allowedOrigins.Length > 0)
    {
        // Any header and method: Dev Tunnels adds X-Tunnel-Skip-AntiPhishing-Page, and the preflight must allow it.
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().SetPreflightMaxAge(TimeSpan.FromHours(1));
    }
}));

builder.Services.AddExceptionHandler<UniqueViolationExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = PortugueseModelBindingMessages.TranslateProblem);
// Make the generated OpenAPI schemas describe enums as strings, matching the MVC JSON options above.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<OperationSecurityTransformer>();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // First admin (Seed:Admin) and terms v1.0. Skipped with a warning until the migrations are applied.
    await DevelopmentDataSeeder.SeedAsync(app.Services);

    // Default page: / and /docs both open the journeys at /docs/journeys.html (the full single-page guide stays at /docs/index.pt.html).
    // Swagger UI: /swagger  |  Raw OpenAPI document: /openapi/v1.json
    app.MapOpenApi();
    app.UseStaticFiles();
    app.MapGet("/", () => Results.Redirect("/docs/journeys.html")).ExcludeFromDescription();
    app.MapGet("/docs", () => Results.Redirect("/docs/journeys.html")).ExcludeFromDescription();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Frota Online API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Frota Online API";
        options.EnablePersistAuthorization();
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors(AppCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

public partial class Program
{
}
