using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Domain;

public class ProposalTests
{
    private static Proposal Create(decimal price = 250m) =>
        Proposal.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), price, null, DateTime.UtcNow.AddMinutes(5));

    [Fact]
    public void NonPositivePrice_IsRejected()
    {
        Assert.Throws<DomainException>(() => Create(0m));
    }

    [Fact]
    public void AcceptedProposal_CannotBeRejected()
    {
        var proposal = Create();
        proposal.Accept();

        Assert.Equal(ProposalStatus.Accepted, proposal.Status);
        Assert.Throws<DomainException>(proposal.Reject);
    }
}

public class DeliveryTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    private static Delivery Create() =>
        Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 300m);

    [Fact]
    public void NewDelivery_ChargesTheFlatPlatformFee()
    {
        Assert.Equal(PlatformFees.PerFreight, Create().PlatformFee);
        Assert.Equal(4.90m, Create().PlatformFee);
    }

    [Fact]
    public void HappyPath_ReachesDelivered()
    {
        var delivery = Create();

        delivery.Advance(DeliveryStatus.ToPickup, Now);
        delivery.Advance(DeliveryStatus.PickedUp, Now);
        delivery.Advance(DeliveryStatus.InTransit, Now);
        delivery.Advance(DeliveryStatus.Delivered, Now.AddHours(1));

        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(Now.AddHours(1), delivery.DeliveredAt);
    }

    [Fact]
    public void SkippingSteps_IsRejected()
    {
        var delivery = Create();

        Assert.Throws<DomainException>(() => delivery.Advance(DeliveryStatus.Delivered, Now));
    }

    [Fact]
    public void DeliveredDelivery_IsTerminal()
    {
        var delivery = Create();
        delivery.Advance(DeliveryStatus.ToPickup, Now);
        delivery.Advance(DeliveryStatus.PickedUp, Now);
        delivery.Advance(DeliveryStatus.InTransit, Now);
        delivery.Advance(DeliveryStatus.Delivered, Now);

        Assert.Throws<DomainException>(() => delivery.Advance(DeliveryStatus.Cancelled, Now));
    }
}

public class DriverProfileTests
{
    [Fact]
    public void Lifecycle_RejectThenResubmitThenApprove()
    {
        var reviewer = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var driver = DriverProfile.Create(Guid.NewGuid(), "12345678901", "99999999999", "c");

        Assert.False(driver.CanOperate);
        driver.SubmitForReview();
        driver.Reject(reviewer, "Blurry CNH photo", now);
        Assert.Equal(DriverStatus.Rejected, driver.Status);

        driver.SubmitForReview();
        Assert.Null(driver.RejectionReason);
        driver.Approve(reviewer, now);

        Assert.True(driver.CanOperate);
    }

    [Fact]
    public void DraftDriver_CannotBeApproved()
    {
        var driver = DriverProfile.Create(Guid.NewGuid(), "12345678901", "99999999999", "C");

        Assert.Throws<DomainException>(() => driver.Approve(Guid.NewGuid(), DateTime.UtcNow));
    }
}
