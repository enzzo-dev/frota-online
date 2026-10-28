using FrotaOnline.Application.Abstractions.Addresses;
using FrotaOnline.Application.Abstractions.Auth;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Marketplace;
using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Abstractions.Terms;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Privacy;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Abstractions.Routing;
using FrotaOnline.Application.Abstractions.Storage;
using FrotaOnline.Application.Auth;
using FrotaOnline.Application.Terms;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Abstractions.Users;
using FrotaOnline.Infrastructure.Background;
using FrotaOnline.Infrastructure.Notifications;
using FrotaOnline.Infrastructure.Payments;
using FrotaOnline.Infrastructure.Persistence;
using FrotaOnline.Infrastructure.Privacy;
using FrotaOnline.Infrastructure.Queries;
using FrotaOnline.Infrastructure.Repositories;
using FrotaOnline.Infrastructure.Routing;
using FrotaOnline.Infrastructure.Security;
using FrotaOnline.Infrastructure.Services;
using FrotaOnline.Infrastructure.Storage;
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

        services.AddDbContextPool<AppDbContext>(options => options.UseNpgsql(connectionString));

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
        services.AddScoped<ISignatureRepository, SignatureRepository>();
        services.AddScoped<ICargoPhotoRepository, CargoPhotoRepository>();
        services.AddScoped<IDeliveryLocationRepository, DeliveryLocationRepository>();
        services.AddScoped<IDriverAgendaRepository, DriverAgendaRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IFeeLedgerRepository, FeeLedgerRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IFeeChargeRepository, FeeChargeRepository>();
        services.AddScoped<ISavedCardRepository, SavedCardRepository>();
        services.AddScoped<IWalletAccountRepository, WalletAccountRepository>();
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();

        // Read side: IQueryable projections executed by PostgreSQL.
        services.AddScoped<IFreightQueries, FreightQueries>();
        services.AddScoped<IDeliveryQueries, DeliveryQueries>();
        services.AddScoped<IFeeQueries, FeeQueries>();
        services.AddScoped<INotificationQueries, NotificationQueries>();
        services.AddScoped<IMatchingDriverQueries, MatchingDriverQueries>();
        services.AddScoped<IAddressUsageQueries, AddressUsageQueries>();
        services.AddScoped<IDriverReviewQueries, DriverReviewQueries>();
        services.AddScoped<ITermAcceptanceChecker, SqlTermAcceptanceChecker>();
        services.AddScoped<IPersonalDataQueries, PersonalDataQueries>();
        services.AddScoped<IAccountErasureStore, AccountErasureStore>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Road routing. To use another engine, write a class that implements IRoutingProvider and register it here.
        services.AddOptions<RoutingOptions>()
            .Bind(configuration.GetSection(RoutingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHttpClient<OsrmRoutingProvider>();
        services.AddSingleton<RouteCache>();
        services.AddScoped<IRoutingProvider>(sp =>
            new CachedRoutingProvider(sp.GetRequiredService<OsrmRoutingProvider>(), sp.GetRequiredService<RouteCache>()));

        // File storage. Storage:Provider = "Gcs" uploads through signed URLs to a private bucket; anything else disables it.
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName));
        if (string.Equals(configuration["Storage:Provider"], "Gcs", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(configuration["Storage:Bucket"]))
            {
                throw new InvalidOperationException("Storage:Bucket is required when Storage:Provider is Gcs.");
            }

            services.AddSingleton<IFileStorage, GcsFileStorage>();
        }
        else
        {
            services.AddSingleton<IFileStorage, NoFileStorage>();
        }

        // Online payments (Asaas). Payments:Provider = Asaas turns on subscriptions, the Pix with split and the fee Pix;
        // anything else leaves them answering "not configured".
        services.Configure<PaymentOptions>(configuration.GetSection(PaymentOptions.SectionName));
        services.AddSingleton<ISubscriptionSettings, SubscriptionSettings>();
        services.AddSingleton<IPaymentWebhookAuthenticator, AsaasWebhookAuthenticator>();
        if (string.Equals(configuration["Payments:Provider"], "Asaas", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(configuration["Payments:Asaas:ApiKey"]))
            {
                throw new InvalidOperationException("Payments:Asaas:ApiKey is required when Payments:Provider is Asaas.");
            }

            services.AddHttpClient<IPaymentGateway, AsaasPaymentGateway>();
        }
        else
        {
            services.AddSingleton<IPaymentGateway, NoPaymentGateway>();
        }

        // Wallet payments (Apple Pay) and the driver's account connection, by Mercado Pago. Needs the OAuth application, the
        // webhook secret and the key that encrypts the drivers' tokens.
        if (string.Equals(configuration["Payments:WalletProvider"], "MercadoPago", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var key in new[] { "ClientId", "ClientSecret", "RedirectUri", "WebhookSecret", "TokenEncryptionKey" })
            {
                if (string.IsNullOrWhiteSpace(configuration[$"Payments:MercadoPago:{key}"]))
                {
                    throw new InvalidOperationException($"Payments:MercadoPago:{key} is required when Payments:WalletProvider is MercadoPago.");
                }
            }

            services.AddHttpClient<IWalletPaymentGateway, MercadoPagoWalletGateway>();
            services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
            services.AddSingleton<IWalletWebhookAuthenticator, MercadoPagoWebhookAuthenticator>();
        }
        else
        {
            services.AddSingleton<IWalletPaymentGateway, NoWalletPaymentGateway>();
            services.AddSingleton<ISecretProtector, DisabledSecretProtector>();
            services.AddSingleton<IWalletWebhookAuthenticator, DisabledWalletWebhookAuthenticator>();
        }

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordResetTokenService, HmacPasswordResetTokenService>();
        services.AddSingleton(configuration.GetSection(PasswordResetOptions.SectionName).Get<PasswordResetOptions>() ?? new PasswordResetOptions());
        if (string.Equals(configuration["Email:Provider"], "MailerSend", StringComparison.OrdinalIgnoreCase))
        {
            services.AddOptions<MailerSendOptions>()
                .Bind(configuration.GetSection(MailerSendOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddHttpClient<IEmailSender, MailerSendEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }
        // Push delivery. To use Firebase (FCM) or another provider, write a class that implements IPushSender and register it here.
        // Push.Provider = "Firebase" sends real pushes (credentials come from Application Default Credentials, so on
        // GCP the service account of the runtime is used); anything else only logs them.
        if (string.Equals(configuration["Push:Provider"], "Firebase", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IPushSender, FirebasePushSender>();
        }
        else
        {
            services.AddSingleton<IPushSender, LoggingPushSender>();
        }

        // Background jobs. In production (Cloud Run) they are triggered by Cloud Scheduler through
        // /internal/jobs/*, because a hosted service is not reliable when instances scale to zero or run in parallel.
        // Jobs:RunInProcess = true runs them inside the API instead (local development, single instance).
        if (configuration.GetValue<bool>("Jobs:RunInProcess"))
        {
            services.AddHostedService<FreightExpirationWorker>();
            services.AddHostedService<NotificationDispatchWorker>();
        }

        return services;
    }
}
