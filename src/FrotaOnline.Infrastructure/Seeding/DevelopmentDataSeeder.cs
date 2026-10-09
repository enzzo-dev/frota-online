using FrotaOnline.Application.Abstractions.Auth;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FrotaOnline.Infrastructure.Seeding;

public static class DevelopmentDataSeeder
{
    /// <summary>
    /// Creates the first admin (from <c>Seed:Admin</c>) and version 1.0 of the terms when they are missing.
    /// Meant for Development only. It never throws: a database without migrations just logs a warning.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentDataSeeder");

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            await SeedAdminAsync(db, configuration, hasher, logger, cancellationToken);
            await SeedTermsAsync(db, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Development seed skipped. Apply the migrations first (dotnet ef database update) and restart.");
        }
    }

    private static async Task SeedAdminAsync(
        AppDbContext db, IConfiguration configuration, IPasswordHasher hasher, ILogger logger, CancellationToken cancellationToken)
    {
        var email = configuration["Seed:Admin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["Seed:Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return;
        }

        var fullName = configuration["Seed:Admin:FullName"] ?? "Frota Online Admin";
        await db.Users.AddAsync(User.Create(fullName, email, "+5511900000000", hasher.Hash(password), UserRole.Admin), cancellationToken);
        logger.LogInformation("Seeded development admin {Email}.", email);
    }

    private static async Task SeedTermsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.TermDocuments.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        await db.TermDocuments.AddRangeAsync(new[]
        {
            TermDocument.Create(TermType.TermsOfUse, "1.0", "Termos de Uso", "https://frotaonline.example/termos/1.0", now),
            TermDocument.Create(TermType.PrivacyPolicy, "1.0", "Política de Privacidade", "https://frotaonline.example/privacidade/1.0", now)
        }, cancellationToken);
    }
}
