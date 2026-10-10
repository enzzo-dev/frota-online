using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Domain;

public class FreightRequestTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    private static FreightStopDraft Stop(StopType type, Guid? addressId = null, params FreightItemDraft[] items) =>
        new(type, addressId ?? Guid.NewGuid(), null, items);

    private static FreightRequest Create(DateTime? scheduledPickupAt) =>
        Create(scheduledPickupAt, Stop(StopType.Pickup), Stop(StopType.Delivery));

    private static FreightRequest Create(DateTime? scheduledPickupAt, params FreightStopDraft[] stops) => FreightRequest.Create(
        Guid.NewGuid(), stops, "Boxes", 120m, VehicleType.Van, scheduledPickupAt, Now);

    [Fact]
    public void Stops_KeepTheOrderAndTheFinalDeliveryIsTheLast()
    {
        var freight = Create(null,
            Stop(StopType.Pickup), Stop(StopType.Pickup), Stop(StopType.Delivery, items: new[] { new FreightItemDraft("Pecas", 3) }), Stop(StopType.Delivery));

        Assert.Equal(new[] { 1, 2, 3, 4 }, freight.Stops.Select(x => x.Sequence));
        Assert.Equal(2, freight.PickupCount);
        Assert.Equal(2, freight.DeliveryCount);
        Assert.Equal(StopType.Delivery, freight.Stops[^1].Type);
        Assert.Equal(3, Assert.Single(freight.Stops[2].Items).Quantity);
    }

    [Fact]
    public void FirstStopThatIsNotAPickup_IsRejected() =>
        Assert.Throws<DomainException>(() => Create(null, Stop(StopType.Delivery), Stop(StopType.Delivery)));

    [Fact]
    public void LastStopThatIsNotADelivery_IsRejected() =>
        Assert.Throws<DomainException>(() => Create(null, Stop(StopType.Pickup), Stop(StopType.Pickup)));

    [Fact]
    public void FewerThanTwoStops_IsRejected() =>
        Assert.Throws<DomainException>(() => Create(null, Stop(StopType.Pickup)));

    [Fact]
    public void RepeatedAddress_IsRejected()
    {
        var address = Guid.NewGuid();

        Assert.Throws<DomainException>(() => Create(null, Stop(StopType.Pickup, address), Stop(StopType.Delivery, address)));
    }

    [Fact]
    public void ItemWithQuantityBelowOne_IsRejected() =>
        Assert.Throws<DomainException>(() =>
            Create(null, Stop(StopType.Pickup), Stop(StopType.Delivery, items: new[] { new FreightItemDraft("Pecas", 0) })));

    [Fact]
    public void AgendaWindow_GrowsWithTheNumberOfStops()
    {
        Assert.Equal(TimeSpan.FromHours(4), DeliveryPlanning.AgendaWindow(2));
        Assert.Equal(TimeSpan.FromHours(6), DeliveryPlanning.AgendaWindow(4));
    }

    [Fact]
    public void SameDay_FreightHasFiveMinuteProposalWindow()
    {
        var freight = Create(null);

        Assert.True(freight.IsSameDay);
        Assert.Equal(Now.AddMinutes(5), freight.ProposalDeadline);
    }

    [Fact]
    public void Scheduled_FreightHasTwentyFourHourWindow()
    {
        var freight = Create(Now.AddDays(3));

        Assert.Equal(Now.AddHours(24), freight.ProposalDeadline);
    }

    [Fact]
    public void Scheduled_FreightDeadlineNeverPassesPickupTime()
    {
        var freight = Create(Now.AddHours(2));

        Assert.Equal(Now.AddHours(2), freight.ProposalDeadline);
    }

    [Fact]
    public void ScheduledInThePast_IsRejected()
    {
        Assert.Throws<DomainException>(() => Create(Now.AddMinutes(-1)));
    }

    [Fact]
    public void ExpireIfDue_ExpiresOnlyAfterDeadline()
    {
        var freight = Create(null);

        Assert.False(freight.ExpireIfDue(Now.AddMinutes(4)));
        Assert.True(freight.ExpireIfDue(Now.AddMinutes(5)));
        Assert.Equal(FreightStatus.Expired, freight.Status);
    }

    [Fact]
    public void Approve_RaisesEventAndBlocksSecondApproval()
    {
        var freight = Create(null);
        var proposalId = Guid.NewGuid();

        freight.Approve(proposalId, Now);

        Assert.Equal(FreightStatus.Approved, freight.Status);
        Assert.Equal(proposalId, freight.ApprovedProposalId);
        Assert.Single(freight.DomainEvents);
        Assert.Throws<DomainException>(() => freight.Approve(Guid.NewGuid(), Now));
    }
}
