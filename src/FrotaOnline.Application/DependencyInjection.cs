using FluentValidation;
using FrotaOnline.Application.Admin;
using FrotaOnline.Application.Addresses;
using FrotaOnline.Application.Auth;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Deliveries;
using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Privacy;
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
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<IFreightService, FreightService>();
        services.AddScoped<IDriverMarketplaceService, DriverMarketplaceService>();
        services.AddScoped<IDriverDeliveryService, DriverDeliveryService>();
        services.AddScoped<IProposalSelectionService, ProposalSelectionService>();
        services.AddScoped<IFreightExpirationService, FreightExpirationService>();
        services.AddScoped<IPrivacyService, PrivacyService>();
        services.AddScoped<IDeliveryProgressService, DeliveryProgressService>();
        services.AddScoped<IDeliveryPaymentService, DeliveryPaymentService>();
        services.AddScoped<IFeeService, FeeService>();
        services.AddScoped<ICustomerDeliveryService, CustomerDeliveryService>();
        return services;
    }
}
