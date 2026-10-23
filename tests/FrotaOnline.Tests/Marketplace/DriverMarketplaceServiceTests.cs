using FrotaOnline.Application.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Marketplace;

public class DriverMarketplaceServiceTests : MarketplaceTestBase
{
    [Fact]
    public async Task Submit_ValidProposal_ExpiresWithTheFreightWindow()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();

        var result = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle, 180.50m, " Chego em 20 min "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProposalStatus.Pending, result.Value.Status);
        Assert.Equal(Freights.Items.Single().ProposalDeadline, result.Value.ExpiresAt);
        Assert.Equal("Chego em 20 min", result.Value.Message);
    }

    [Fact]
    public async Task Submit_ByDriverNotApproved_ReturnsForbidden()
    {
        var freightId = await CreateFreightAsync();
        var userId = Guid.NewGuid();
        Drivers.Items.Add(DriverProfile.Create(userId, "52998224701", "12345678901", "B"));

        var result = await DriverMarketplace.SubmitProposalAsync(
            userId, freightId, new(Guid.NewGuid(), 100m, null), CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task Submit_WithVehicleOfAnotherType_IsRejected()
    {
        var freightId = await CreateFreightAsync(type: VehicleType.Fiorino);
        var (userId, _, van) = AddApprovedDriver(type: VehicleType.Van);

        var result = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(van), CancellationToken.None);

        Assert.Equal("proposal.vehicle_not_eligible", result.Error!.Code);
    }

    [Fact]
    public async Task Submit_WithVehicleThatCannotCarryTheCargo_IsRejected()
    {
        var freightId = await CreateFreightAsync(weight: 900);
        var (userId, _, vehicle) = AddApprovedDriver(capacity: 600);

        var result = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);

        Assert.Equal("proposal.vehicle_not_eligible", result.Error!.Code);
    }

    [Fact]
    public async Task Submit_WithVehicleOfAnotherDriver_IsRejected()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, _) = AddApprovedDriver("AAA1A11");
        var (_, _, otherVehicle) = AddApprovedDriver("BBB2B22");

        var result = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(otherVehicle), CancellationToken.None);

        Assert.Equal("proposal.vehicle_not_eligible", result.Error!.Code);
    }

    [Fact]
    public async Task Submit_Twice_ReturnsConflict()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();
        await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);

        var result = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle, 120m), CancellationToken.None);

        Assert.Equal("proposal.already_submitted", result.Error!.Code);
    }

    [Fact]
    public async Task Submit_AfterTheWindowEnds_ReturnsConflict()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();
        Clock.Advance(TimeSpan.FromMinutes(6));

        var result = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);

        Assert.Equal("freight.window_closed", result.Error!.Code);
    }

    [Fact]
    public async Task Submit_WhenAgendaIsBusy_ReturnsConflict()
    {
        var freightId = await CreateFreightAsync();
        var (userId, profile, vehicle) = AddApprovedDriver();
        Agenda.Items.Add(DriverAgendaSlot.Create(profile.Id, Guid.NewGuid(), Clock.UtcNow.AddHours(-1), Clock.UtcNow.AddHours(2)));

        var result = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);

        Assert.Equal("proposal.driver_busy", result.Error!.Code);
    }

    [Fact]
    public async Task Submit_WithZeroOrThreeDecimalPrice_IsRejectedByValidation()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();

        var zero = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle, 0m), CancellationToken.None);
        var precise = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle, 10.123m), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, zero.Error!.Type);
        Assert.Equal(ErrorType.Validation, precise.Error!.Type);
    }

    [Fact]
    public async Task ListOpen_ShowsOnlyFreightsThatFitTheDriversVehicles_WithoutStreets()
    {
        await CreateFreightAsync(type: VehicleType.Fiorino);
        await CreateFreightAsync(type: VehicleType.Truck);
        var (userId, _, _) = AddApprovedDriver(type: VehicleType.Fiorino);

        var result = await DriverMarketplace.ListOpenFreightsAsync(userId, CancellationToken.None);

        var item = Assert.Single(result.Value);
        Assert.Equal(VehicleType.Fiorino, item.RequiredVehicleType);
        Assert.Equal("Centro", item.Stops[0].District);
        Assert.Equal("Campinas", item.Stops[1].City);
    }

    [Fact]
    public async Task ListOpen_HidesFreightsHeavierThanTheDriversVehicle()
    {
        await CreateFreightAsync(weight: 300);
        await CreateFreightAsync(weight: 900);
        var (userId, _, _) = AddApprovedDriver(capacity: 600);

        var result = await DriverMarketplace.ListOpenFreightsAsync(userId, CancellationToken.None);

        var item = Assert.Single(result.Value);
        Assert.Equal(300m, item.CargoWeightKg);
    }

    [Fact]
    public async Task ListOpen_ShowsMotorcycleFreightsOnlyToMotorcycleDrivers()
    {
        await CreateFreightAsync(type: VehicleType.Motorcycle, weight: 10);
        await CreateFreightAsync(type: VehicleType.Fiorino, weight: 10);
        var (motoUser, _, _) = AddApprovedDriver("MOT1A23", VehicleType.Motorcycle, capacity: 30);

        var result = await DriverMarketplace.ListOpenFreightsAsync(motoUser, CancellationToken.None);

        var item = Assert.Single(result.Value);
        Assert.Equal(VehicleType.Motorcycle, item.RequiredVehicleType);
    }

    [Fact]
    public async Task ListOpen_HidesFreightsTheDriverAlreadyProposedOn()
    {
        var proposed = await CreateFreightAsync();
        var other = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();
        await DriverMarketplace.SubmitProposalAsync(userId, proposed, Proposal(vehicle, 100m), CancellationToken.None);

        var result = await DriverMarketplace.ListOpenFreightsAsync(userId, CancellationToken.None);

        var item = Assert.Single(result.Value);
        Assert.Equal(other, item.Id);
    }

    [Fact]
    public async Task ListOpen_ShowsStopCountsAndItemsPerStop_ButNoStreets()
    {
        var a = AddAddress(CustomerId, "Sao Paulo");
        var b = AddAddress(CustomerId, "Campinas", "Cambui", "13010000");
        var c = AddAddress(CustomerId, "Santos", "Gonzaga", "11050000");
        var stops = new[]
        {
            Stop(StopType.Pickup, a),
            Stop(StopType.Delivery, b, items: new[] { ("Caixas", 5) }),
            Stop(StopType.Delivery, c, items: new[] { ("Pecas", 2) })
        };
        await FreightService.CreateAsync(CustomerId, NewFreight(stops), CancellationToken.None);
        var (userId, _, _) = AddApprovedDriver();

        var result = await DriverMarketplace.ListOpenFreightsAsync(userId, CancellationToken.None);

        var freight = Assert.Single(result.Value);
        Assert.Equal(1, freight.PickupCount);
        Assert.Equal(2, freight.DeliveryCount);
        Assert.Equal(new[] { "Sao Paulo", "Campinas", "Santos" }, freight.Stops.Select(s => s.City));
        Assert.Equal("Caixas", Assert.Single(freight.Stops[1].Items).Description);
    }

    [Fact]
    public async Task ListOpen_ByDriverNotApproved_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        Drivers.Items.Add(DriverProfile.Create(userId, "52998224701", "12345678901", "B"));

        var result = await DriverMarketplace.ListOpenFreightsAsync(userId, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task Withdraw_PendingProposal_Succeeds_ThenCannotBeWithdrawnAgain()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver();
        var proposal = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);

        var first = await DriverMarketplace.WithdrawProposalAsync(userId, proposal.Value.Id, CancellationToken.None);
        var second = await DriverMarketplace.WithdrawProposalAsync(userId, proposal.Value.Id, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(ErrorType.Conflict, second.Error!.Type);
    }

    [Fact]
    public async Task Withdraw_ProposalOfAnotherDriver_ReturnsNotFound()
    {
        var freightId = await CreateFreightAsync();
        var (userId, _, vehicle) = AddApprovedDriver("AAA1A11");
        var (otherUserId, _, _) = AddApprovedDriver("BBB2B22");
        var proposal = await DriverMarketplace.SubmitProposalAsync(userId, freightId, Proposal(vehicle), CancellationToken.None);

        var result = await DriverMarketplace.WithdrawProposalAsync(otherUserId, proposal.Value.Id, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }
}
