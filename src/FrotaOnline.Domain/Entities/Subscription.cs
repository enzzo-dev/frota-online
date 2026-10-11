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

    /// <summary>Id of the recurring charge at the payment provider.</summary>
    public string? GatewaySubscriptionId { get; private set; }

    /// <summary>Hosted page where the driver pays the current charge (Pix, boleto or card). Never holds card data on our side.</summary>
    public string? PaymentUrl { get; private set; }

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

    /// <summary>
    /// True while the driver is inside a period they paid for. A cancelled plan keeps working until that period ends.
    /// </summary>
    public bool HasAccess(DateTime utcNow) =>
        Status != SubscriptionStatus.Pending && StartsAt is not null && EndsAt is not null && utcNow < EndsAt;

    public void LinkGateway(string gatewaySubscriptionId, string? paymentUrl)
    {
        GatewaySubscriptionId = Guard.NotNullOrWhiteSpace(gatewaySubscriptionId, nameof(gatewaySubscriptionId));
        PaymentUrl = paymentUrl;
    }

    /// <summary>
    /// A charge was paid: the plan is active and runs one more month, counted from the end of the current period
    /// when the driver paid early (no days are lost), or from the payment when it had lapsed.
    /// </summary>
    public void RegisterPayment(DateTime paidAt)
    {
        var from = EndsAt is { } end && end > paidAt ? end : paidAt;
        Status = SubscriptionStatus.Active;
        StartsAt ??= paidAt;
        EndsAt = from.AddMonths(1);
    }

    /// <summary>Stops future charges but keeps the access already paid for.</summary>
    public void CancelAtPeriodEnd(DateTime utcNow)
    {
        Status = SubscriptionStatus.Cancelled;
        if (EndsAt is null || EndsAt < utcNow)
        {
            EndsAt = utcNow;
        }
    }

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
