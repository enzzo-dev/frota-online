using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FrotaOnline.Api.Extensions;

public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>The exact URL (or audience) Cloud Scheduler puts in the OIDC token, normally the service URL.</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>E-mail of the service account Cloud Scheduler runs as. Only tokens from it are accepted.</summary>
    public string ServiceAccountEmail { get; init; } = string.Empty;

    /// <summary>Runs the jobs inside the API process instead of waiting for Cloud Scheduler (development).</summary>
    public bool RunInProcess { get; init; }
}

public static class InternalJobsAuthentication
{
    public const string Scheme = "GoogleOidc";
    public const string Policy = "InternalJobs";

    /// <summary>
    /// Cloud Scheduler calls the internal job endpoints with a Google-signed OIDC token. The token is validated
    /// against Google's public keys and the call is accepted only for the configured service account. With
    /// <c>Jobs:ServiceAccountEmail</c> empty, every call is refused.
    /// </summary>
    public static IServiceCollection AddInternalJobsAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JobsOptions>().Bind(configuration.GetSection(JobsOptions.SectionName));

        services.AddAuthentication().AddJwtBearer(Scheme, _ => { });
        services.AddOptions<JwtBearerOptions>(Scheme)
            .Configure<IOptions<JobsOptions>>((bearer, jobs) =>
            {
                bearer.Authority = "https://accounts.google.com";
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuers = new[] { "https://accounts.google.com", "accounts.google.com" },
                    ValidateAudience = true,
                    ValidAudience = jobs.Value.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddSingleton<IAuthorizationHandler, InternalJobsHandler>();
        services.AddAuthorizationBuilder().AddPolicy(Policy, policy => policy
            .AddAuthenticationSchemes(Scheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new InternalJobsRequirement()));

        return services;
    }
}

internal sealed class InternalJobsRequirement : IAuthorizationRequirement
{
}

internal sealed class InternalJobsHandler : AuthorizationHandler<InternalJobsRequirement>
{
    private readonly IOptions<JobsOptions> _options;

    public InternalJobsHandler(IOptions<JobsOptions> options) => _options = options;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, InternalJobsRequirement requirement)
    {
        var allowed = _options.Value.ServiceAccountEmail;
        var email = context.User.FindFirst("email")?.Value;
        var verified = context.User.FindFirst("email_verified")?.Value;

        if (!string.IsNullOrWhiteSpace(allowed)
            && string.Equals(email, allowed, StringComparison.OrdinalIgnoreCase)
            && string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
