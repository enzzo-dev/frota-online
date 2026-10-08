using FluentValidation;
using FrotaOnline.Application.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace FrotaOnline.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
