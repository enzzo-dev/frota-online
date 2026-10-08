using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Domain;

public class FreightRequestTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    private static FreightRequest Create(DateTime? scheduledPickupAt) => FreightRequest.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Boxes", 120m, VehicleType.Van, scheduledPickupAt, Now);

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
