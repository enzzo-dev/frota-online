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
        builder.HasIndex(x => x.DeliveryId);
        builder.Property(x => x.ExternalReference).HasMaxLength(128);
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
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverProfileId);
    }
}
