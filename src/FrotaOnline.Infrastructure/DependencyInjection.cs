using FrotaOnline.Application.Abstractions.Addresses;
using FrotaOnline.Application.Abstractions.Auth;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Application.Abstractions.Terms;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Privacy;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Terms;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Abstractions.Users;
using FrotaOnline.Infrastructure.Background;
using FrotaOnline.Infrastructure.Notifications;
using FrotaOnline.Infrastructure.Persistence;
using FrotaOnline.Infrastructure.Privacy;
using FrotaOnline.Infrastructure.Queries;
using FrotaOnline.Infrastructure.Repositories;
using FrotaOnline.Infrastructure.Security;
using FrotaOnline.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FrotaOnline.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured for this environment. " +
                "Set it in appsettings.{Environment}.json (Development) or through the " +
                "ConnectionStrings__Default environment variable / secret manager (Staging, Production).");
        }

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<ITermRepository, TermRepository>();
        services.AddScoped<IConsentRepository, ConsentRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IMediaFileRepository, MediaFileRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IFreightRepository, FreightRepository>();
        services.AddScoped<IProposalRepository, ProposalRepository>();
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<IDriverAgendaRepository, DriverAgendaRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IFeeLedgerRepository, FeeLedgerRepository>();

        // Read side: IQueryable projections executed by PostgreSQL.
        services.AddScoped<IFreightQueries, FreightQueries>();
        services.AddScoped<IDeliveryQueries, DeliveryQueries>();
        services.AddScoped<IFeeQueries, FeeQueries>();
        services.AddScoped<IDriverReviewQueries, DriverReviewQueries>();
        services.AddScoped<ITermAcceptanceChecker, SqlTermAcceptanceChecker>();
        services.AddScoped<IPersonalDataQueries, PersonalDataQueries>();
        services.AddScoped<IAccountErasureStore, AccountErasureStore>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        services.AddHostedService<FreightExpirationWorker>();

        return services;
    }
}
