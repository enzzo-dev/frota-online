using FrotaOnline.Application.Common;
using FrotaOnline.Application.Deliveries;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Marketplace;

namespace FrotaOnline.Tests.Deliveries;

public class DeliveryProgressServiceTests : MarketplaceTestBase
{
    private static readonly CompleteStopRequest Plain = new(null, null, null, null);

    [Fact]
    public async Task Start_MovesScheduledToPickup_AndWritesAnEvent()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();

        var result = await Progress.StartAsync(userId, deliveryId, CancellationToken.None);

        Assert.Equal(DeliveryStatus.ToPickup, result.Value.Status);
        Assert.Contains(Deliveries.Events, e => e.Status == DeliveryStatus.ToPickup);
    }

    [Fact]
    public async Task Start_Twice_ReturnsConflict()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);

        var result = await Progress.StartAsync(userId, deliveryId, CancellationToken.None);

        Assert.Equal("delivery.invalid_status", result.Error!.Code);
    }

    [Fact]
    public async Task Start_ByAnotherDriver_ReturnsNotFound()
    {
        var (_, _, deliveryId, _) = await ScheduleDeliveryAsync();
        var (otherUser, _, _) = AddApprovedDriver("ZZZ9Z99", name: "Outro");

        var result = await Progress.StartAsync(otherUser, deliveryId, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task CompleteStop_BeforeStarting_ReturnsConflict()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync();

        var result = await Progress.CompleteStopAsync(userId, deliveryId, stops[0], Plain, CancellationToken.None);

        Assert.Equal("delivery.invalid_status", result.Error!.Code);
    }

    [Fact]
    public async Task CompleteStop_OutOfOrder_ReturnsConflict()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync(deliveryStops: 2);
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);

        var result = await Progress.CompleteStopAsync(userId, deliveryId, stops[1], Plain, CancellationToken.None);

        Assert.Equal("delivery.stop_out_of_order", result.Error!.Code);
    }

    [Fact]
    public async Task CompleteStop_WithAStopOfAnotherDelivery_ReturnsNotFound()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);

        var result = await Progress.CompleteStopAsync(userId, deliveryId, Guid.NewGuid(), Plain, CancellationToken.None);

        Assert.Equal("delivery.stop_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task CompleteStop_FirstPickup_PicksUpTheCargo()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);

        var result = await Progress.CompleteStopAsync(userId, deliveryId, stops[0], Plain, CancellationToken.None);

        Assert.Equal(DeliveryStatus.InTransit, result.Value.Status);
        Assert.Equal(1, result.Value.CompletedStops);
        Assert.Equal(stops[1], result.Value.NextStopId);
        Assert.NotNull(Deliveries.Items.Single().PickedUpAt);
        Assert.Empty(Fees.Items);
    }

    [Fact]
    public async Task CompleteStop_LastStop_Delivers_AccruesTheFee_AndReleasesTheAgenda()
    {
        var (userId, profile, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], Plain, CancellationToken.None);

        var result = await Progress.CompleteStopAsync(
            userId, deliveryId, stops[1], new CompleteStopRequest("Left at the dock", -23.5, -46.6, null), CancellationToken.None);

        Assert.Equal(DeliveryStatus.Delivered, result.Value.Status);
        Assert.Null(result.Value.NextStopId);
        Assert.NotNull(Deliveries.Items.Single().DeliveredAt);

        var fee = Assert.Single(Fees.Items);
        Assert.Equal(profile.Id, fee.DriverProfileId);
        Assert.Equal(deliveryId, fee.DeliveryId);
        Assert.Equal(FeeEntryType.FeeAccrued, fee.EntryType);
        Assert.Equal(4.90m, fee.Amount);

        Assert.False(Agenda.Items.Single().IsActive);

        var last = Deliveries.Events.Single(e => e.Status == DeliveryStatus.Delivered);
        Assert.Equal("Left at the dock", last.Note);
        Assert.Equal(-23.5, last.Latitude);
    }

    [Fact]
    public async Task CompleteStop_MultiStopRun_StaysInTransitUntilTheLastStop()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync(deliveryStops: 2);
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);

        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], Plain, CancellationToken.None);
        var middle = await Progress.CompleteStopAsync(userId, deliveryId, stops[1], Plain, CancellationToken.None);
        var last = await Progress.CompleteStopAsync(userId, deliveryId, stops[2], Plain, CancellationToken.None);

        Assert.Equal(DeliveryStatus.InTransit, middle.Value.Status);
        Assert.Equal(DeliveryStatus.Delivered, last.Value.Status);
        Assert.Single(Fees.Items);
    }

    [Fact]
    public async Task CompleteStop_AfterDelivered_ReturnsConflict()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], Plain, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[1], Plain, CancellationToken.None);

        var result = await Progress.CompleteStopAsync(userId, deliveryId, stops[1], Plain, CancellationToken.None);

        Assert.Equal("delivery.invalid_status", result.Error!.Code);
        Assert.Single(Fees.Items);
    }

    [Fact]
    public async Task CompleteStop_WithAProofOfAnotherUser_ReturnsValidation()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        var foreign = MediaFile.Create(Guid.NewGuid(), MediaKind.DeliveryProof, "proofs/x.jpg", "image/jpeg", 100);
        Media.Items.Add(foreign);

        var result = await Progress.CompleteStopAsync(
            userId, deliveryId, stops[0], new CompleteStopRequest(null, null, null, foreign.Id), CancellationToken.None);

        Assert.Equal("delivery.invalid_proof", result.Error!.Code);
        Assert.False(Freights.Items.Single().Stops.First().IsCompleted);
    }

    [Fact]
    public async Task CompleteStop_WithTheDriversProof_RecordsItOnTheEvent()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], Plain, CancellationToken.None);
        var proof = MediaFile.Create(userId, MediaKind.DeliveryProof, "proofs/ok.jpg", "image/jpeg", 100);
        Media.Items.Add(proof);

        await Progress.CompleteStopAsync(
            userId, deliveryId, stops[1], new CompleteStopRequest(null, null, null, proof.Id), CancellationToken.None);

        Assert.Equal(proof.Id, Deliveries.Events.Single(e => e.Status == DeliveryStatus.Delivered).MediaFileId);
    }

    [Fact]
    public async Task CompleteStop_WithOnlyOneCoordinate_ReturnsValidation()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);

        var result = await Progress.CompleteStopAsync(
            userId, deliveryId, stops[0], new CompleteStopRequest(null, -23.5, null, null), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Incident_ThenCancel_ReleasesTheAgenda_AndAccruesNoFee()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], Plain, CancellationToken.None);

        var incident = await Progress.ReportIncidentAsync(userId, deliveryId, new ReportIncidentRequest("Flat tire"), CancellationToken.None);
        var cancel = await Progress.CancelAsDriverAsync(userId, deliveryId, new CancelDeliveryRequest("Truck broke"), CancellationToken.None);

        Assert.Equal(DeliveryStatus.Incident, incident.Value.Status);
        Assert.Equal(DeliveryStatus.Cancelled, cancel.Value.Status);
        Assert.False(Agenda.Items.Single().IsActive);
        Assert.Empty(Fees.Items);
    }

    [Fact]
    public async Task Incident_WhileScheduled_ReturnsConflict()
    {
        var (userId, _, deliveryId, _) = await ScheduleDeliveryAsync();

        var result = await Progress.ReportIncidentAsync(userId, deliveryId, new ReportIncidentRequest("x"), CancellationToken.None);

        Assert.Equal("delivery.invalid_status", result.Error!.Code);
    }

    [Fact]
    public async Task DriverCancel_InTransit_ReturnsConflict()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], Plain, CancellationToken.None);

        var result = await Progress.CancelAsDriverAsync(userId, deliveryId, new CancelDeliveryRequest("Changed my mind"), CancellationToken.None);

        Assert.Equal("delivery.invalid_status", result.Error!.Code);
    }

    [Fact]
    public async Task CustomerCancel_BeforePickup_Works_AndFreesTheDriver()
    {
        var (_, _, deliveryId, _) = await ScheduleDeliveryAsync();

        var result = await Progress.CancelAsCustomerAsync(CustomerId, deliveryId, new CancelDeliveryRequest("No longer needed"), CancellationToken.None);

        Assert.Equal(DeliveryStatus.Cancelled, result.Value.Status);
        Assert.False(Agenda.Items.Single().IsActive);
        Assert.Contains("cliente", Deliveries.Events.Last().Note);
    }

    [Fact]
    public async Task CustomerCancel_AfterPickup_ReturnsConflict()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync();
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], Plain, CancellationToken.None);

        var result = await Progress.CancelAsCustomerAsync(CustomerId, deliveryId, new CancelDeliveryRequest("Too late"), CancellationToken.None);

        Assert.Equal("delivery.invalid_status", result.Error!.Code);
    }

    [Fact]
    public async Task CustomerCancel_OfAnotherCustomersDelivery_ReturnsNotFound()
    {
        var (_, _, deliveryId, _) = await ScheduleDeliveryAsync();

        var result = await Progress.CancelAsCustomerAsync(Guid.NewGuid(), deliveryId, new CancelDeliveryRequest("x"), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }
}
