using FluentValidation;
using FrotaOnline.Application.Admin;
using FrotaOnline.Application.Auth;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Terms;
using Microsoft.Extensions.DependencyInjection;

namespace FrotaOnline.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITermService, TermService>();
        services.AddScoped<IDriverProfileService, DriverProfileService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IDriverDocumentService, DriverDocumentService>();
        services.AddScoped<IDriverReviewService, DriverReviewService>();
        return services;
    }
}
