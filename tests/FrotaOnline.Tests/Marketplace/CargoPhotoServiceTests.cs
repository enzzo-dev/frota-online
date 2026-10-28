using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace;
using FrotaOnline.Application.Marketplace.Dtos;
using FrotaOnline.Application.Marketplace.Validators;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Marketplace;

public class CargoPhotoServiceTests : MarketplaceTestBase
{
    private CargoPhotoService Service => new(
        Freights, Deliveries, Drivers, CargoPhotos, UnitOfWork, new UploadCargoPhotoRequestValidator());

    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static string Image(int size = 2048)
    {
        var bytes = new byte[size];
        PngHeader.CopyTo(bytes, 0);
        return Convert.ToBase64String(bytes);
    }

    private Task<Result> UploadAsync(Guid userId, Guid freightId, string? image = null) =>
        Service.UploadAsync(userId, freightId, new UploadCargoPhotoRequest(image ?? Image()), CancellationToken.None);

    [Fact]
    public async Task Customer_AttachesAPhoto_AndItIsStoredCompressed()
    {
        var freightId = await CreateFreightAsync();

        var result = await UploadAsync(CustomerId, freightId, Image(100_000));

        var saved = Assert.Single(CargoPhotos.Items);
        Assert.True(result.IsSuccess);
        Assert.Equal("image/png", saved.ContentType);
        Assert.Equal(100_000, saved.SizeBytes);
        Assert.True(saved.ImageData.Length < 100_000);
    }

    [Fact]
    public async Task Photo_ReadsBackIdentical_AndFreightReportsHasCargoPhoto()
    {
        var freightId = await CreateFreightAsync();
        var image = Image();
        await UploadAsync(CustomerId, freightId, "data:image/png;base64," + image);

        var read = await Service.GetAsync(CustomerId, freightId, CancellationToken.None);
        var freight = await FreightService.GetAsync(CustomerId, freightId, CancellationToken.None);

        Assert.Equal(Convert.FromBase64String(image), read.Value.Content);
        Assert.Equal("image/png", read.Value.ContentType);
        Assert.True(freight.Value.HasCargoPhoto);
    }

    [Fact]
    public async Task FreightWithoutPhoto_ReportsFalse_AndGetReturnsNotFound()
    {
        var freightId = await CreateFreightAsync();

        var freight = await FreightService.GetAsync(CustomerId, freightId, CancellationToken.None);
        var read = await Service.GetAsync(CustomerId, freightId, CancellationToken.None);

        Assert.False(freight.Value.HasCargoPhoto);
        Assert.Equal("cargo_photo.not_found", read.Error!.Code);
    }

    [Fact]
    public async Task SecondPhoto_ReturnsConflict()
    {
        var freightId = await CreateFreightAsync();
        await UploadAsync(CustomerId, freightId);

        var again = await UploadAsync(CustomerId, freightId);

        Assert.Equal("cargo_photo.already_exists", again.Error!.Code);
    }

    [Fact]
    public async Task AnotherCustomer_CannotAttachOrRead()
    {
        var freightId = await CreateFreightAsync();
        var stranger = Guid.NewGuid();

        var upload = await UploadAsync(stranger, freightId);
        var read = await Service.GetAsync(stranger, freightId, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, upload.Error!.Type);
        Assert.Equal(ErrorType.NotFound, read.Error!.Type);
    }

    [Fact]
    public async Task ClosedFreight_RefusesNewPhoto()
    {
        var freightId = await CreateFreightAsync();
        await FreightService.CancelAsync(CustomerId, freightId, CancellationToken.None);

        var result = await UploadAsync(CustomerId, freightId);

        Assert.Equal("freight.not_open", result.Error!.Code);
    }

    [Fact]
    public async Task InvalidBase64_TooLarge_AndNonImage_AreRejected()
    {
        var freightId = await CreateFreightAsync();

        var invalid = await UploadAsync(CustomerId, freightId, "isto nao e base64!");
        var large = await UploadAsync(CustomerId, freightId, Image(CargoPhotoLimits.MaxSizeBytes + 1));
        var notImage = await UploadAsync(CustomerId, freightId, Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

        Assert.Equal("cargo_photo.invalid_image", invalid.Error!.Code);
        Assert.Equal("cargo_photo.file_too_large", large.Error!.Code);
        Assert.Equal("cargo_photo.file_type_not_allowed", notImage.Error!.Code);
        Assert.Empty(CargoPhotos.Items);
    }

    [Fact]
    public async Task ApprovedDriver_SeesThePhotoOfAnOpenFreight_AndOpenListReportsIt()
    {
        var freightId = await CreateFreightAsync();
        await UploadAsync(CustomerId, freightId);
        var (userId, _, _) = AddApprovedDriver();

        var read = await Service.GetAsync(userId, freightId, CancellationToken.None);
        var open = await DriverMarketplace.ListOpenFreightsAsync(userId, CancellationToken.None);

        Assert.True(read.IsSuccess);
        Assert.True(Assert.Single(open.Value).HasCargoPhoto);
    }

    [Fact]
    public async Task DriverWithoutAccess_ReturnsNotFound()
    {
        var freightId = await CreateFreightAsync();
        await UploadAsync(CustomerId, freightId);
        var userId = Guid.NewGuid();
        Drivers.Items.Add(FrotaOnline.Domain.Entities.DriverProfile.Create(userId, "52998224701", "12345678901", "B"));

        var read = await Service.GetAsync(userId, freightId, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, read.Error!.Type);
    }

    [Fact]
    public async Task DriverOfTheDelivery_StillSeesThePhotoAfterApproval_AndRouteCarriesNoteAndFlag()
    {
        var (driverUserId, _, deliveryId, _) = await ScheduleDeliveryAsync();
        var freightId = Deliveries.Items.Single().FreightRequestId;
        CargoPhotos.Items.Add(FrotaOnline.Domain.Entities.FreightCargoPhoto.Create(freightId, new byte[] { 1 }, "image/png", 1));

        var read = await Service.GetAsync(driverUserId, freightId, CancellationToken.None);
        var route = await DriverDelivery.GetRouteAsync(driverUserId, deliveryId, RouteView.Full, CancellationToken.None);

        Assert.True(read.IsSuccess);
        Assert.True(route.Value.HasCargoPhoto);
        Assert.Equal(Freights.Items.Single().DriverNote, route.Value.DriverNote);
    }

    [Fact]
    public async Task DriverNote_IsSavedTrimmed_AndShownToTheDriver()
    {
        var (pickup, delivery) = AddAddresses(CustomerId);
        var request = NewFreight(pickup, delivery) with { DriverNote = "  Tocar o interfone  " };
        var created = await FreightService.CreateAsync(CustomerId, request, CancellationToken.None);
        var (userId, _, _) = AddApprovedDriver();

        var open = await DriverMarketplace.ListOpenFreightsAsync(userId, CancellationToken.None);

        Assert.Equal("Tocar o interfone", created.Value.DriverNote);
        Assert.Equal("Tocar o interfone", Assert.Single(open.Value).DriverNote);
    }

    [Fact]
    public async Task DriverNote_Over300Characters_IsRejected()
    {
        var (pickup, delivery) = AddAddresses(CustomerId);
        var request = NewFreight(pickup, delivery) with { DriverNote = new string('a', 301) };

        var result = await FreightService.CreateAsync(CustomerId, request, CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task DriverDeliverySummary_CarriesAgendaCargoPlacesVehicleAndNet()
    {
        var (driverUserId, _, deliveryId, _) = await ScheduleDeliveryAsync();

        var summary = await DriverDelivery.GetAsync(driverUserId, deliveryId, CancellationToken.None);

        var delivery = Deliveries.Items.Single();
        var value = summary.Value;
        Assert.NotNull(value.AgendaStartsAt);
        Assert.True(value.AgendaEndsAt > value.AgendaStartsAt);
        Assert.Equal("Caixas de pecas", value.CargoDescription);
        Assert.False(string.IsNullOrEmpty(value.OriginCity));
        Assert.False(string.IsNullOrEmpty(value.DestinationCity));
        Assert.False(string.IsNullOrEmpty(value.VehiclePlate));
        Assert.Null(value.DeliveredAt);
        Assert.Equal(delivery.PlatformFee, value.PlatformFee);
        Assert.Equal(delivery.AgreedPrice - delivery.PlatformFee, value.DriverNet);
    }
}
