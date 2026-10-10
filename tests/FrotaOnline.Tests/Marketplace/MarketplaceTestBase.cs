using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Application.Marketplace.Validators;
using FrotaOnline.Application.Addresses.Validators;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Auth;
using FrotaOnline.Tests.Onboarding;

namespace FrotaOnline.Tests.Marketplace;

public abstract class MarketplaceTestBase
{
    private protected readonly FakeClock Clock = new();
    private protected readonly FakeUnitOfWork UnitOfWork = new();
    private protected readonly FakeUserRepository Users = new();
    private protected readonly FakeDriverRepository Drivers = new();
    private protected readonly FakeVehicleRepository Vehicles = new();
    private protected readonly FakeAddressRepository Addresses = new();
    private protected readonly FakeFreightRepository Freights = new();
    private protected readonly FakeProposalRepository Proposals = new();
    private protected readonly FakeDeliveryRepository Deliveries = new();
    private protected readonly FakeDriverAgendaRepository Agenda = new();
    private protected readonly FakeTermAcceptanceChecker Terms = new();

    protected readonly Guid CustomerId = Guid.NewGuid();

    protected FreightService FreightService => new(
        Freights, Proposals, Addresses, Terms, UnitOfWork, Clock, new CreateFreightRequestValidator());

    protected DriverMarketplaceService DriverMarketplace => new(
        Drivers, Vehicles, Freights, Proposals, Addresses, Agenda, UnitOfWork, Clock, new SubmitProposalRequestValidator());

    protected ProposalSelectionService Selection => new(
        Freights, Proposals, Drivers, Vehicles, Users, Deliveries, Agenda, UnitOfWork, Clock);

    protected FreightExpirationService Expiration => new(Freights, Proposals, UnitOfWork, Clock);

    protected (Address Pickup, Address Delivery) AddAddresses(Guid ownerId)
    {
        var pickup = Address.Create(ownerId, "Casa", "Rua A", "10", null, "Centro", "Sao Paulo", "SP", "01001000", null, null);
        var delivery = Address.Create(ownerId, "Loja", "Rua B", "20", null, "Mooca", "Campinas", "SP", "13010000", null, null);
        Addresses.Items.Add(pickup);
        Addresses.Items.Add(delivery);
        return (pickup, delivery);
    }

    protected CreateFreightRequest NewFreight(
        Address pickup, Address delivery, VehicleType type = VehicleType.Fiorino, DateTime? scheduled = null, decimal weight = 300) =>
        new(pickup.Id, delivery.Id, "Caixas de pecas", weight, type, scheduled);

    protected async Task<Guid> CreateFreightAsync(DateTime? scheduled = null, VehicleType type = VehicleType.Fiorino, decimal weight = 300)
    {
        var (pickup, delivery) = AddAddresses(CustomerId);
        var result = await FreightService.CreateAsync(CustomerId, NewFreight(pickup, delivery, type, scheduled, weight), CancellationToken.None);
        return result.Value.Id;
    }

    protected (Guid UserId, DriverProfile Profile, Vehicle Vehicle) AddApprovedDriver(
        string plate = "ABC1D23", VehicleType type = VehicleType.Fiorino, decimal capacity = 600, string name = "Joao Silva")
    {
        var user = User.Create(name, $"{plate.ToLowerInvariant()}@example.com", "+5511999999999", "hash", UserRole.Driver);
        Users.Items.Add(user);

        var digits = (Drivers.Items.Count + 1).ToString("D2");
        var profile = DriverProfile.Create(user.Id, "529982247" + digits, "123456789" + digits, "B");
        profile.SubmitForReview();
        profile.Approve(Guid.NewGuid(), Clock.UtcNow);
        Drivers.Items.Add(profile);

        var vehicle = Vehicle.Create(profile.Id, type, plate, "Fiat Fiorino", 2020, capacity);
        Vehicles.Items.Add(vehicle);
        return (user.Id, profile, vehicle);
    }

    protected static SubmitProposalRequest Proposal(Vehicle vehicle, decimal price = 150m, string? message = null) =>
        new(vehicle.Id, price, message);
}
