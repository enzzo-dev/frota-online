using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FrotaOnline.Infrastructure.Persistence.Configurations;

internal sealed class FreightRequestConfiguration : IEntityTypeConfiguration<FreightRequest>
{
    public void Configure(EntityTypeBuilder<FreightRequest> builder)
    {
        builder.HasIndex(x => new { x.Status, x.ProposalDeadline });
        builder.HasIndex(x => x.CustomerId);
        builder.Property(x => x.CargoDescription).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CargoWeightKg).HasPrecision(12, 2);
        builder.Property(x => x.DriverNote).HasMaxLength(FreightLimits.MaxDriverNoteLength);
        builder.Property(x => x.RoutePolyline).HasColumnType("text");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId);
        builder.HasMany(x => x.Stops).WithOne().HasForeignKey(s => s.FreightRequestId);
        builder.Navigation(x => x.Stops).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class FreightStopConfiguration : IEntityTypeConfiguration<FreightStop>
{
    public void Configure(EntityTypeBuilder<FreightStop> builder)
    {
        builder.HasIndex(x => new { x.FreightRequestId, x.Sequence }).IsUnique();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<Address>().WithMany().HasForeignKey(x => x.AddressId);
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.FreightStopId);
        builder.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class FreightItemConfiguration : IEntityTypeConfiguration<FreightItem>
{
    public void Configure(EntityTypeBuilder<FreightItem> builder)
    {
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
    }
}

internal sealed class ProposalConfiguration : IEntityTypeConfiguration<Proposal>
{
    public void Configure(EntityTypeBuilder<Proposal> builder)
    {
        builder.HasIndex(x => new { x.FreightRequestId, x.Price });
        // Serves the expiration sweep: WHERE Status = 'Pending' AND ExpiresAt <= now.
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
        // A driver can have only one pending proposal per freight.
        builder.HasIndex(x => new { x.FreightRequestId, x.DriverProfileId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
        builder.Property(x => x.Message).HasMaxLength(500);
        builder.HasOne<FreightRequest>().WithMany().HasForeignKey(x => x.FreightRequestId);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverProfileId);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId);
    }
}

internal sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.HasIndex(x => x.FreightRequestId).IsUnique();
        builder.HasIndex(x => new { x.DriverProfileId, x.Status });
        // The customer's list is "my deliveries, newest first": filter and order served by one index.
        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt });
        builder.HasOne<FreightRequest>().WithMany().HasForeignKey(x => x.FreightRequestId);
        builder.HasOne<Proposal>().WithMany().HasForeignKey(x => x.ProposalId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverProfileId);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId);
    }
}

internal sealed class DeliveryEventConfiguration : IEntityTypeConfiguration<DeliveryEvent>
{
    public void Configure(EntityTypeBuilder<DeliveryEvent> builder)
    {
        builder.HasIndex(x => new { x.DeliveryId, x.OccurredAt });
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasOne<Delivery>().WithMany().HasForeignKey(x => x.DeliveryId);
        builder.HasOne<MediaFile>().WithMany().HasForeignKey(x => x.MediaFileId);
    }
}

internal sealed class DeliveryLocationConfiguration : IEntityTypeConfiguration<DeliveryLocation>
{
    public void Configure(EntityTypeBuilder<DeliveryLocation> builder)
    {
        builder.HasIndex(x => x.DeliveryId).IsUnique();
        builder.HasOne<Delivery>().WithMany().HasForeignKey(x => x.DeliveryId);
    }
}

internal sealed class DriverAgendaSlotConfiguration : IEntityTypeConfiguration<DriverAgendaSlot>
{
    public void Configure(EntityTypeBuilder<DriverAgendaSlot> builder)
    {
        builder.HasIndex(x => new { x.DriverProfileId, x.StartsAt, x.EndsAt });
        builder.Ignore(x => x.IsActive);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverProfileId);
        builder.HasOne<Delivery>().WithMany().HasForeignKey(x => x.DeliveryId);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        // One payment per delivery: a second confirmation must fail even under concurrent requests.
        builder.HasIndex(x => x.DeliveryId).IsUnique();
        builder.Property(x => x.ExternalReference).HasMaxLength(128);
        builder.Property(x => x.GatewayPaymentId).HasMaxLength(64);
        builder.Property(x => x.Gateway).HasMaxLength(32);
        builder.Property(x => x.PixPayload).HasMaxLength(1024);
        builder.Property(x => x.PixQrCode).HasMaxLength(16384);
        builder.HasIndex(x => x.GatewayPaymentId).HasFilter("\"GatewayPaymentId\" IS NOT NULL");
        builder.HasOne<Delivery>().WithMany().HasForeignKey(x => x.DeliveryId);
    }
}

internal sealed class FeeLedgerConfiguration : IEntityTypeConfiguration<FeeLedger>
{
    public void Configure(EntityTypeBuilder<FeeLedger> builder)
    {
        builder.HasIndex(x => x.DriverProfileId);
        builder.Property(x => x.Description).HasMaxLength(300);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverProfileId);
        builder.HasOne<Delivery>().WithMany().HasForeignKey(x => x.DeliveryId);
    }
}

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.HasIndex(x => new { x.DriverProfileId, x.Status });
        builder.Property(x => x.PhoneNumber).HasMaxLength(20);
        builder.Property(x => x.GatewaySubscriptionId).HasMaxLength(64);
        builder.Property(x => x.PaymentUrl).HasMaxLength(512);
        builder.HasIndex(x => x.GatewaySubscriptionId).HasFilter("\"GatewaySubscriptionId\" IS NOT NULL");
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverProfileId);
    }
}

internal sealed class FeeChargeConfiguration : IEntityTypeConfiguration<FeeCharge>
{
    public void Configure(EntityTypeBuilder<FeeCharge> builder)
    {
        builder.Property(x => x.GatewayPaymentId).HasMaxLength(64);
        builder.Property(x => x.PixPayload).HasMaxLength(1024);
        builder.Property(x => x.PixQrCode).HasMaxLength(16384);
        builder.HasIndex(x => x.GatewayPaymentId).IsUnique();
        builder.HasIndex(x => new { x.DriverProfileId, x.Status });
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverProfileId);
    }
}

internal sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.Property(x => x.EventId).HasMaxLength(128);
        builder.Property(x => x.EventType).HasMaxLength(64);
        builder.HasIndex(x => x.EventId).IsUnique();
    }
}

internal sealed class SavedCardConfiguration : IEntityTypeConfiguration<SavedCard>
{
    public void Configure(EntityTypeBuilder<SavedCard> builder)
    {
        builder.Property(x => x.GatewayCustomerId).HasMaxLength(64);
        builder.Property(x => x.Token).HasMaxLength(128);
        builder.Property(x => x.Brand).HasMaxLength(32);
        builder.Property(x => x.Last4).HasMaxLength(4);
        builder.HasIndex(x => x.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
    }
}

internal sealed class WalletAccountConfiguration : IEntityTypeConfiguration<WalletAccount>
{
    public void Configure(EntityTypeBuilder<WalletAccount> builder)
    {
        builder.Property(x => x.ProviderUserId).HasMaxLength(64);
        builder.Property(x => x.AccessTokenProtected).HasMaxLength(2048);
        builder.Property(x => x.RefreshTokenProtected).HasMaxLength(2048);
        // One connected account per driver, and one driver per provider account.
        builder.HasIndex(x => x.DriverProfileId).IsUnique();
        builder.HasIndex(x => x.ProviderUserId).IsUnique();
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverProfileId);
    }
}

internal sealed class FreightViewConfiguration : IEntityTypeConfiguration<FreightView>
{
    public void Configure(EntityTypeBuilder<FreightView> builder)
    {
        builder.HasIndex(x => new { x.FreightRequestId, x.DriverProfileId }).IsUnique();
        builder.HasIndex(x => new { x.DriverProfileId, x.FirstViewedAt });
        builder.HasOne<FreightRequest>().WithMany().HasForeignKey(x => x.FreightRequestId);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverProfileId);
    }
}

internal sealed class FreightCargoPhotoConfiguration : IEntityTypeConfiguration<FreightCargoPhoto>
{
    public void Configure(EntityTypeBuilder<FreightCargoPhoto> builder)
    {
        builder.HasIndex(x => x.FreightRequestId).IsUnique();
        builder.Property(x => x.ImageData).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(32).IsRequired();
        builder.HasOne<FreightRequest>().WithMany().HasForeignKey(x => x.FreightRequestId);
    }
}

internal sealed class SignatureConfiguration : IEntityTypeConfiguration<Signature>
{
    public void Configure(EntityTypeBuilder<Signature> builder)
    {
        builder.HasIndex(x => new { x.FreightRequestId, x.Type }).IsUnique();
        builder.Property(x => x.ImageData).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(32).IsRequired();
        builder.HasOne<FreightRequest>().WithMany().HasForeignKey(x => x.FreightRequestId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverProfileId);
    }
}
