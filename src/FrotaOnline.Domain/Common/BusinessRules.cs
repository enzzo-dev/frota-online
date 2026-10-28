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
    /// <summary>Fixed time reserved in the driver's agenda, starting at the first pickup.</summary>
    public static readonly TimeSpan BaseWindow = TimeSpan.FromHours(2);

    /// <summary>Extra time reserved for each stop, pickups and deliveries alike.</summary>
    public static readonly TimeSpan PerStop = TimeSpan.FromHours(1);

    /// <summary>Agenda time for a freight with the given number of stops (a pickup plus a delivery reserves 4 hours).</summary>
    public static TimeSpan AgendaWindow(int stopCount) => BaseWindow + PerStop * Math.Max(2, stopCount);
}

public static class FreightLimits
{
    public const int MaxStops = 20;

    public const int MaxItemsPerStop = 50;

    public const int MaxDriverNoteLength = 300;
}

public static class SubscriptionPricing
{
    public const decimal Basic = 79.90m;

    public const decimal Full = 99.90m;

    public const int BasicMaxVehicles = 3;
}
