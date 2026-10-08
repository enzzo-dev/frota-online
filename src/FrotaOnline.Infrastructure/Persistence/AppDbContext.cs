using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FrotaOnline.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<TermDocument> TermDocuments => Set<TermDocument>();
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();
    public DbSet<FreightRequest> FreightRequests => Set<FreightRequest>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryEvent> DeliveryEvents => Set<DeliveryEvent>();
    public DbSet<DriverAgendaSlot> DriverAgendaSlots => Set<DriverAgendaSlot>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<FeeLedger> FeeLedgers => Set<FeeLedger>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)).ToList())
        {
            modelBuilder.Entity(entityType.ClrType).Ignore(nameof(BaseEntity.DomainEvents));
        }

        // Never cascade deletes: financial and audit records must survive.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>().Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.Touch(now);
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
