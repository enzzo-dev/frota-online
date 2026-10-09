using FrotaOnline.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FrotaOnline.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.PhoneNumber);
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
    }
}

internal sealed class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
    }
}

internal sealed class DriverProfileConfiguration : IEntityTypeConfiguration<DriverProfile>
{
    public void Configure(EntityTypeBuilder<DriverProfile> builder)
    {
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CpfNumber).IsUnique();
        builder.HasIndex(x => x.CnhNumber).IsUnique();
        builder.Property(x => x.CpfNumber).HasMaxLength(11).IsRequired();
        builder.Property(x => x.CnhNumber).HasMaxLength(11).IsRequired();
        builder.Property(x => x.CnhCategory).HasMaxLength(2).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
    }
}

internal sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.HasIndex(x => x.OwnerUserId);
        builder.Property(x => x.State).HasMaxLength(2);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId);
    }
}

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.HasIndex(x => x.Plate).IsUnique();
        builder.Property(x => x.Plate).HasMaxLength(10);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverProfileId);
    }
}

internal sealed class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.OwnerUserId, x.Kind });
        builder.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId);
    }
}

internal sealed class TermDocumentConfiguration : IEntityTypeConfiguration<TermDocument>
{
    public void Configure(EntityTypeBuilder<TermDocument> builder)
    {
        builder.HasIndex(x => new { x.Type, x.Version }).IsUnique();
        builder.Property(x => x.ContentUrl).HasMaxLength(1024);
    }
}

internal sealed class UserConsentConfiguration : IEntityTypeConfiguration<UserConsent>
{
    public void Configure(EntityTypeBuilder<UserConsent> builder)
    {
        builder.HasIndex(x => new { x.UserId, x.TermDocumentId }).IsUnique();
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        builder.HasOne<TermDocument>().WithMany().HasForeignKey(x => x.TermDocumentId);
    }
}

internal sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.HasIndex(x => x.Token).IsUnique();
        builder.Property(x => x.Token).HasMaxLength(512);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
    }
}
