namespace FrotaOnline.Domain.Common;

public static class PlatformFees
{
    /// <summary>Flat operational fee charged per freight (BRL).</summary>
    public const decimal PerFreight = 4.90m;
}

public static class ProposalWindows
{
    public static readonly TimeSpan SameDay = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan Scheduled = TimeSpan.FromHours(24);
}

public static class DeliveryPlanning
{
    /// <summary>Time reserved in the driver's agenda for one delivery, starting at the pickup.</summary>
    public static readonly TimeSpan AgendaWindow = TimeSpan.FromHours(4);
}

public static class SubscriptionPricing
{
    public const decimal Basic = 79.90m;

    public const decimal Full = 99.90m;

    public const int BasicMaxVehicles = 3;
}
