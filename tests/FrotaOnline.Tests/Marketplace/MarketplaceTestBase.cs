using FrotaOnline.Application.Deliveries;
using FrotaOnline.Application.Deliveries.Dtos;
using FrotaOnline.Application.Deliveries.Validators;
using FrotaOnline.Application.Addresses;
using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Notifications;
using FrotaOnline.Application.Payments;
using FrotaOnline.Tests.Payments;
using FrotaOnline.Application.Routing;
using FrotaOnline.Application.Routing.Validators;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Application.Marketplace.Validators;
using FrotaOnline.Application.Addresses.Validators;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Auth;
using FrotaOnline.Tests.Notifications;
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
    private protected readonly FakeDeliveryLocationRepository Locations = new();
    private protected readonly FakeDriverAgendaRepository Agenda = new();
    private protected readonly FakeTermAcceptanceChecker Terms = new();
    private protected readonly FakeMediaFileRepository Media = new();
    private protected readonly FakeSignatureRepository Signatures = new();
    private protected readonly FakePaymentRepository Payments = new();
    private protected readonly FakeFeeLedgerRepository Fees = new();
    private protected readonly FakeRoutingProvider Routing = new();
    private protected readonly FakeNotificationRepository SentNotifications = new();
    private protected readonly FakeNotificationPreferenceRepository Preferences = new();
    private protected readonly FakeSubscriptionRepository Subscriptions = new();
    private protected readonly FakeSubscriptionSettings SubscriptionRules = new();
    private protected readonly FakePaymentGateway Gateway = new();

    private protected NotificationPublisher Publisher => new(SentNotifications);

    private protected FakeFreightQueries FreightQueries => new(Freights, Proposals, Addresses, Drivers, Users, Vehicles);
    private protected FakeDeliveryQueries DeliveryQueries => new(Deliveries, Freights, Addresses, Drivers, Users, Vehicles, Payments);

    protected readonly Guid CustomerId = Guid.NewGuid();

    protected FreightService FreightService => new(
        Freights, Proposals, Addresses, FreightQueries, Terms, new RoutePlanner(Routing),
        new FakeMatchingDriverQueries(Drivers, Vehicles, Agenda, Preferences), Publisher,
        UnitOfWork, Clock, new CreateFreightRequestValidator());

    protected NotificationPreferenceService PreferenceService => new(Preferences, UnitOfWork);

    protected AddressService AddressService => new(
        Addresses, new FakeAddressUsageQueries(Freights, Deliveries), UnitOfWork, Clock, new CreateAddressRequestValidator());

    protected RouteService RouteService => new(
        Addresses, Freights, Deliveries, Drivers, new RoutePlanner(Routing), new RoutePreviewRequestValidator());

    protected DriverMarketplaceService DriverMarketplace => new(
        Drivers, Vehicles, Freights, Proposals, FreightQueries, Agenda, SubscriptionPolicy, Publisher, UnitOfWork, Clock, new SubmitProposalRequestValidator());

    private protected SubscriptionPolicy SubscriptionPolicy => new(Subscriptions, SubscriptionRules, Deliveries, Clock);

    protected ProposalSelectionService Selection => new(
        Freights, Proposals, Drivers, Vehicles, FreightQueries, Deliveries, Agenda, SubscriptionPolicy, Publisher, UnitOfWork, Clock);

    protected DriverDeliveryService DriverDelivery => new(Drivers, DeliveryQueries);

    protected DeliveryProgressService Progress => new(
        Drivers, Deliveries, Freights, Agenda, Media, Signatures, Fees, Publisher, UnitOfWork, Clock,
        new CompleteStopRequestValidator(), new ReportIncidentRequestValidator(), new CancelDeliveryRequestValidator());

    protected DeliveryLocationService LocationService => new(
        Drivers, Deliveries, Locations, UnitOfWork, Clock, new UpdateLocationRequestValidator());

    protected DeliveryPaymentService PaymentService => new(
        Drivers, Deliveries, Payments, Publisher, UnitOfWork, Clock, new ConfirmPaymentRequestValidator());

    protected FeeService FeeService => new(
        Drivers, Fees, new FakeFeeQueries(Fees), Publisher, UnitOfWork, new SettleFeeRequestValidator());

    protected CustomerDeliveryService CustomerDeliveries => new(DeliveryQueries);

    protected FreightExpirationService Expiration => new(Freights, Proposals, Drivers, Publisher, UnitOfWork, Clock);

    protected (Address Pickup, Address Delivery) AddAddresses(Guid ownerId)
    {
        var pickup = Address.Create(ownerId, "Casa", "Rua A", "10", null, "Centro", "Sao Paulo", "SP", "01001000", null, null);
        var delivery = Address.Create(ownerId, "Loja", "Rua B", "20", null, "Mooca", "Campinas", "SP", "13010000", null, null);
        Addresses.Items.Add(pickup);
        Addresses.Items.Add(delivery);
        return (pickup, delivery);
    }

    protected Address AddAddress(
        Guid ownerId, string city, string district = "Centro", string zip = "01001000", double? latitude = null, double? longitude = null)
    {
        var address = Address.Create(ownerId, city, "Rua " + city, "1", null, district, city, "SP", zip, latitude, longitude);
        Addresses.Items.Add(address);
        return address;
    }

    protected static CreateFreightStopRequest Stop(
        StopType type, Address address, string? note = null, params (string Description, int Quantity)[] items) =>
        new(type, address.Id, note, items.Select(i => new CreateFreightItemRequest(i.Description, i.Quantity)).ToList());

    protected CreateFreightRequest NewFreight(
        Address pickup, Address delivery, VehicleType type = VehicleType.Fiorino, DateTime? scheduled = null, decimal weight = 300) =>
        NewFreight(new[] { Stop(StopType.Pickup, pickup), Stop(StopType.Delivery, delivery) }, type, scheduled, weight);

    protected CreateFreightRequest NewFreight(
        IReadOnlyList<CreateFreightStopRequest> stops, VehicleType type = VehicleType.Fiorino, DateTime? scheduled = null, decimal weight = 300) =>
        new(stops, "Caixas de pecas", weight, type, scheduled);

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

    /// <summary>A freight with the given stops, approved to a new driver. Returns what tests need to drive the run.</summary>
    protected async Task<(Guid UserId, DriverProfile Profile, Guid DeliveryId, IReadOnlyList<Guid> StopIds)> ScheduleDeliveryAsync(
        int deliveryStops = 1, bool signed = true)
    {
        var (pickup, _) = AddAddresses(CustomerId);
        var stops = new List<CreateFreightStopRequest> { Stop(StopType.Pickup, pickup) };
        for (var i = 0; i < deliveryStops; i++)
        {
            stops.Add(Stop(StopType.Delivery, AddAddress(CustomerId, "Cidade" + i, zip: "0100100" + i)));
        }

        var freight = await FreightService.CreateAsync(CustomerId, NewFreight(stops), CancellationToken.None);
        var (userId, profile, vehicle) = AddApprovedDriver();
        var proposal = await DriverMarketplace.SubmitProposalAsync(
            userId, freight.Value.Id, Proposal(vehicle), CancellationToken.None);
        var approved = await Selection.ApproveAsync(CustomerId, freight.Value.Id, proposal.Value.Id, CancellationToken.None);

        var stopIds = Freights.Items.Single().Stops.OrderBy(s => s.Sequence).Select(s => s.Id).ToList();
        if (signed)
        {
            foreach (var type in new[] { SignatureType.Customer, SignatureType.Driver })
            {
                Signatures.Items.Add(Signature.Create(type, freight.Value.Id, CustomerId, profile.Id, $"signatures/{freight.Value.Id:N}/{type}/seed.png"));
            }
        }

        return (userId, profile, approved.Value.DeliveryId, stopIds);
    }
}
