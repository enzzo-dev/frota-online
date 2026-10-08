using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

public sealed class Subscription : BaseEntity
{
    private Subscription()
    {
    }

    public Guid DriverProfileId { get; private set; }

    public SubscriptionPlan Plan { get; private set; }

    public SubscriptionStatus Status { get; private set; } = SubscriptionStatus.Pending;

    public decimal MonthlyPrice { get; private set; }

    public int MaxVehicles { get; private set; }

    /// <summary>The basic plan is tied to the driver's phone number.</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    public DateTime? StartsAt { get; private set; }

    public DateTime? EndsAt { get; private set; }

    public static Subscription Create(Guid driverProfileId, SubscriptionPlan plan, string phoneNumber) => new()
    {
        DriverProfileId = Guard.NotEmpty(driverProfileId, nameof(driverProfileId)),
        Plan = plan,
        PhoneNumber = Guard.NotNullOrWhiteSpace(phoneNumber, nameof(phoneNumber)),
        MonthlyPrice = plan == SubscriptionPlan.Basic ? SubscriptionPricing.Basic : SubscriptionPricing.Full,
        MaxVehicles = plan == SubscriptionPlan.Basic ? SubscriptionPricing.BasicMaxVehicles : int.MaxValue
    };

    public bool IsActive(DateTime utcNow) =>
        Status == SubscriptionStatus.Active && (EndsAt is null || utcNow < EndsAt);

    public void Activate(DateTime startsAt, DateTime? endsAt)
    {
        Status = SubscriptionStatus.Active;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public void Cancel(DateTime utcNow)
    {
        Status = SubscriptionStatus.Cancelled;
        EndsAt = utcNow;
    }
}
