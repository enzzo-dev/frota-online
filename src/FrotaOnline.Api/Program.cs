using System.Text.Json.Serialization;
using FrotaOnline.Api.Extensions;
using FrotaOnline.Api.OpenApi;
using FrotaOnline.Application;
using FrotaOnline.Infrastructure;
using FrotaOnline.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication();

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
// Make the generated OpenAPI schemas describe enums as strings, matching the MVC JSON options above.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<OperationSecurityTransformer>();
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // First admin (Seed:Admin) and terms v1.0. Skipped with a warning until the migrations are applied.
    await DevelopmentDataSeeder.SeedAsync(app.Services);

    // Default page: / and /docs both open the guide at /docs/index.html.
    // Swagger UI: /swagger  |  Raw OpenAPI document: /openapi/v1.json
    app.MapOpenApi();
    app.UseStaticFiles();
    app.MapGet("/", () => Results.Redirect("/docs/index.html")).ExcludeFromDescription();
    app.MapGet("/docs", () => Results.Redirect("/docs/index.html")).ExcludeFromDescription();
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

public partial class Program
{
}
