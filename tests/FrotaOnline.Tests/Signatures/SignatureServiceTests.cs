using FrotaOnline.Application.Common;
using FrotaOnline.Application.Signatures;
using FrotaOnline.Application.Signatures.Dtos;
using FrotaOnline.Application.Signatures.Validators;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Marketplace;

namespace FrotaOnline.Tests.Signatures;

public class SignatureServiceTests : MarketplaceTestBase
{
    private SignatureService Service => new(
        Deliveries, Drivers, Signatures, UnitOfWork, new RegisterSignatureRequestValidator());

    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0 };

    private static string Image(byte[]? header = null, int size = 1024)
    {
        var bytes = new byte[size];
        var prefix = header ?? PngHeader;
        prefix.CopyTo(bytes, 0);
        return Convert.ToBase64String(bytes);
    }

    private async Task<(Guid DriverUserId, Guid FreightId)> ScheduledFreightAsync()
    {
        var (userId, _, _, _) = await ScheduleDeliveryAsync(signed: false);
        return (userId, Deliveries.Items.Single().FreightRequestId);
    }

    private Task SignAsync(Guid userId, SignatureType type, Guid freightId) =>
        Service.RegisterAsync(userId, new RegisterSignatureRequest(type, freightId, Image()), CancellationToken.None);

    [Fact]
    public async Task Customer_SignsTheirFreight_AndTheRowCarriesCustomerDriverAndFreight()
    {
        var (_, freightId) = await ScheduledFreightAsync();
        var result = await Service.RegisterAsync(CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, Image()), CancellationToken.None);

        var delivery = Deliveries.Items.Single();
        var saved = Assert.Single(Signatures.Items);
        Assert.Equal(SignatureType.Customer, result.Value.Type);
        Assert.Equal(freightId, saved.FreightRequestId);
        Assert.Equal(CustomerId, saved.CustomerId);
        Assert.Equal(delivery.DriverProfileId, saved.DriverProfileId);
        Assert.Equal("image/png", saved.ContentType);
        Assert.Equal(1024, saved.SizeBytes);
    }

    [Fact]
    public async Task Driver_SignsTheirFreight()
    {
        var (driverUserId, freightId) = await ScheduledFreightAsync();
        var result = await Service.RegisterAsync(driverUserId, new RegisterSignatureRequest(SignatureType.Driver, freightId, Image()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SignatureType.Driver, Assert.Single(Signatures.Items).Type);
    }

    [Fact]
    public async Task Register_StoresTheImageCompressed_AndReadsItBackIdentical()
    {
        var (_, freightId) = await ScheduledFreightAsync();
        var image = Image(JpegHeader, 100_000);

        var result = await Service.RegisterAsync(
            CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, "data:image/jpeg;base64," + image), CancellationToken.None);
        var read = await Service.GetImageAsync(CustomerId, freightId, SignatureType.Customer, CancellationToken.None);

        var saved = Assert.Single(Signatures.Items);
        Assert.Equal("image/jpeg", result.Value.ContentType);
        Assert.True(saved.ImageData.Length < 100_000);
        Assert.Equal("image/jpeg", read.Value.ContentType);
        Assert.Equal(Convert.FromBase64String(image), read.Value.Content);
    }

    [Fact]
    public async Task Register_WithATooLargeImage_IsRejected()
    {
        var (_, freightId) = await ScheduledFreightAsync();

        var result = await Service.RegisterAsync(
            CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, Image(size: SignatureLimits.MaxSizeBytes + 1)), CancellationToken.None);

        Assert.Equal("signature.file_too_large", result.Error!.Code);
        Assert.Empty(Signatures.Items);
    }

    [Fact]
    public async Task Register_WithANonImageFile_IsRejected()
    {
        var (_, freightId) = await ScheduledFreightAsync();
        var notAnImage = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var result = await Service.RegisterAsync(
            CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, notAnImage), CancellationToken.None);

        Assert.Equal("signature.file_type_not_allowed", result.Error!.Code);
    }

    [Fact]
    public async Task Register_WithInvalidBase64_IsRejected()
    {
        var (_, freightId) = await ScheduledFreightAsync();

        var result = await Service.RegisterAsync(
            CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, "isto nao e base64!"), CancellationToken.None);

        Assert.Equal("signature.invalid_image", result.Error!.Code);
    }

    [Fact]
    public async Task GetImage_ByStranger_ReturnsNotFound()
    {
        var (_, freightId) = await ScheduledFreightAsync();
        await SignAsync(CustomerId, SignatureType.Customer, freightId);

        var result = await Service.GetImageAsync(Guid.NewGuid(), freightId, SignatureType.Customer, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task GetImage_NotCollectedYet_ReturnsNotFound()
    {
        var (_, freightId) = await ScheduledFreightAsync();

        var result = await Service.GetImageAsync(CustomerId, freightId, SignatureType.Driver, CancellationToken.None);

        Assert.Equal("signature.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task CustomerCannotSignAsTheDriver_ReturnsNotFound()
    {
        var (_, freightId) = await ScheduledFreightAsync();

        var result = await Service.RegisterAsync(CustomerId, new RegisterSignatureRequest(SignatureType.Driver, freightId, Image()), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Driver_CollectsTheCustomersSignatureOnTheirPhone()
    {
        var (driverUserId, freightId) = await ScheduledFreightAsync();
        var result = await Service.RegisterAsync(driverUserId, new RegisterSignatureRequest(SignatureType.Customer, freightId, Image()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CustomerId, Assert.Single(Signatures.Items).CustomerId);
    }

    [Fact]
    public async Task StrangerCannotSign_ReturnsNotFound()
    {
        var (_, freightId) = await ScheduledFreightAsync();

        var result = await Service.RegisterAsync(Guid.NewGuid(), new RegisterSignatureRequest(SignatureType.Customer, freightId, Image()), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task UnknownFreight_ReturnsNotFound()
    {
        var result = await Service.RegisterAsync(CustomerId, new RegisterSignatureRequest(SignatureType.Customer, Guid.NewGuid(), Image()), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task SecondSignatureOfTheSameType_ReturnsConflict()
    {
        var (_, freightId) = await ScheduledFreightAsync();
        await SignAsync(CustomerId, SignatureType.Customer, freightId);

        var again = await Service.RegisterAsync(CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, Image()), CancellationToken.None);

        Assert.Equal("signature.already_collected", again.Error!.Code);
    }

    [Fact]
    public async Task CancelledDelivery_ReturnsConflict()
    {
        var (driverUserId, freightId) = await ScheduledFreightAsync();
        await Progress.CancelAsDriverAsync(driverUserId, Deliveries.Items.Single().Id, new FrotaOnline.Application.Deliveries.Dtos.CancelDeliveryRequest("Imprevisto"), CancellationToken.None);

        var result = await Service.RegisterAsync(CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, Image()), CancellationToken.None);

        Assert.Equal("signature.delivery_cancelled", result.Error!.Code);
    }

    [Fact]
    public async Task LastStop_WithoutSignatures_IsBlocked_AndDeliveryStaysInTransit()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync(signed: false);
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], new(null, null, null, null), CancellationToken.None);

        var result = await Progress.CompleteStopAsync(userId, deliveryId, stops[1], new(null, null, null, null), CancellationToken.None);

        Assert.Equal("delivery.signatures_missing", result.Error!.Code);
        Assert.Equal(DeliveryStatus.InTransit, Deliveries.Items.Single().Status);
        Assert.All(Freights.Items.Single().Stops.Where(s => s.Id == stops[1]), s => Assert.False(s.IsCompleted));
    }

    [Fact]
    public async Task LastStop_WithOnlyOneSignature_IsBlocked()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync(signed: false);
        var freightId = Deliveries.Items.Single().FreightRequestId;
        await SignAsync(CustomerId, SignatureType.Customer, freightId);
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], new(null, null, null, null), CancellationToken.None);

        var result = await Progress.CompleteStopAsync(userId, deliveryId, stops[1], new(null, null, null, null), CancellationToken.None);

        Assert.Equal("delivery.signatures_missing", result.Error!.Code);
        Assert.Contains("motorista", result.Error!.Message);
    }

    [Fact]
    public async Task LastStop_WithBothSignatures_DeliversTheFreight()
    {
        var (userId, _, deliveryId, stops) = await ScheduleDeliveryAsync(signed: false);
        var freightId = Deliveries.Items.Single().FreightRequestId;
        await SignAsync(CustomerId, SignatureType.Customer, freightId);
        await SignAsync(userId, SignatureType.Driver, freightId);
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], new(null, null, null, null), CancellationToken.None);

        var result = await Progress.CompleteStopAsync(userId, deliveryId, stops[1], new(null, null, null, null), CancellationToken.None);

        Assert.Equal(DeliveryStatus.Delivered, result.Value.Status);
    }

    [Fact]
    public async Task Status_ExposesWhichSignaturesWereCollected_ToBothSides()
    {
        var (driverUserId, _, deliveryId, _) = await ScheduleDeliveryAsync(signed: false);
        var freightId = Deliveries.Items.Single().FreightRequestId;

        var before = await CustomerDeliveries.GetStatusAsync(CustomerId, deliveryId, CancellationToken.None);
        await SignAsync(driverUserId, SignatureType.Customer, freightId);
        var after = await CustomerDeliveries.GetStatusAsync(CustomerId, deliveryId, CancellationToken.None);
        var driverView = await DriverDelivery.GetAsync(driverUserId, deliveryId, CancellationToken.None);

        Assert.False(before.Value.CustomerSigned);
        Assert.True(after.Value.CustomerSigned);
        Assert.False(after.Value.DriverSigned);
        Assert.NotEqual(before.Value.Version, after.Value.Version);
        Assert.True(driverView.Value.CustomerSigned);
        Assert.False(driverView.Value.DriverSigned);
    }
}
