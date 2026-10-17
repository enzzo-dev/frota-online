using FrotaOnline.Application.Abstractions.Storage;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Signatures;
using FrotaOnline.Application.Signatures.Dtos;
using FrotaOnline.Application.Signatures.Validators;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Marketplace;
using FrotaOnline.Tests.Notifications;

namespace FrotaOnline.Tests.Signatures;

public class SignatureServiceTests : MarketplaceTestBase
{
    private readonly FakeFileStorage _storage = new() { IsConfigured = true };

    private SignatureService Service => new(
        Deliveries, Drivers, Signatures, _storage, UnitOfWork,
        new CreateSignatureUploadUrlRequestValidator(), new RegisterSignatureRequestValidator());

    private async Task<(Guid DriverUserId, Guid FreightId)> ScheduledFreightAsync()
    {
        var (userId, _, _, _) = await ScheduleDeliveryAsync(signed: false);
        return (userId, Deliveries.Items.Single().FreightRequestId);
    }

    private async Task<string> UploadAsync(Guid userId, SignatureType type, Guid freightId, long size = 1024, string contentType = "image/png")
    {
        var ticket = await Service.CreateUploadUrlAsync(userId, new CreateSignatureUploadUrlRequest(type, freightId, contentType), CancellationToken.None);
        _storage.Objects[ticket.Value.StoragePath] = new StoredObject(size, contentType);
        return ticket.Value.StoragePath;
    }

    [Fact]
    public async Task Customer_SignsTheirFreight_AndTheRowCarriesCustomerDriverAndFreight()
    {
        var (_, freightId) = await ScheduledFreightAsync();
        var path = await UploadAsync(CustomerId, SignatureType.Customer, freightId);

        var result = await Service.RegisterAsync(CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, path), CancellationToken.None);

        var delivery = Deliveries.Items.Single();
        var saved = Assert.Single(Signatures.Items);
        Assert.Equal(SignatureType.Customer, result.Value.Type);
        Assert.Equal(freightId, saved.FreightRequestId);
        Assert.Equal(CustomerId, saved.CustomerId);
        Assert.Equal(delivery.DriverProfileId, saved.DriverProfileId);
        Assert.Equal(path, saved.StoragePath);
    }

    [Fact]
    public async Task Driver_SignsTheirFreight()
    {
        var (driverUserId, freightId) = await ScheduledFreightAsync();
        var path = await UploadAsync(driverUserId, SignatureType.Driver, freightId, contentType: "image/jpeg");

        var result = await Service.RegisterAsync(driverUserId, new RegisterSignatureRequest(SignatureType.Driver, freightId, path), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SignatureType.Driver, Assert.Single(Signatures.Items).Type);
    }

    [Fact]
    public async Task CustomerCannotSignAsTheDriver_ReturnsNotFound()
    {
        var (_, freightId) = await ScheduledFreightAsync();

        var result = await Service.CreateUploadUrlAsync(
            CustomerId, new CreateSignatureUploadUrlRequest(SignatureType.Driver, freightId, "image/png"), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Driver_CollectsTheCustomersSignatureOnTheirPhone()
    {
        var (driverUserId, freightId) = await ScheduledFreightAsync();
        var path = await UploadAsync(driverUserId, SignatureType.Customer, freightId);

        var result = await Service.RegisterAsync(driverUserId, new RegisterSignatureRequest(SignatureType.Customer, freightId, path), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CustomerId, Assert.Single(Signatures.Items).CustomerId);
    }

    [Fact]
    public async Task StrangerCannotSign_ReturnsNotFound()
    {
        var (_, freightId) = await ScheduledFreightAsync();

        var result = await Service.CreateUploadUrlAsync(
            Guid.NewGuid(), new CreateSignatureUploadUrlRequest(SignatureType.Customer, freightId, "image/png"), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task UnknownFreight_ReturnsNotFound()
    {
        var result = await Service.CreateUploadUrlAsync(
            CustomerId, new CreateSignatureUploadUrlRequest(SignatureType.Customer, Guid.NewGuid(), "image/png"), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task SecondSignatureOfTheSameType_ReturnsConflict()
    {
        var (_, freightId) = await ScheduledFreightAsync();
        var path = await UploadAsync(CustomerId, SignatureType.Customer, freightId);
        await Service.RegisterAsync(CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, path), CancellationToken.None);

        var again = await Service.CreateUploadUrlAsync(
            CustomerId, new CreateSignatureUploadUrlRequest(SignatureType.Customer, freightId, "image/png"), CancellationToken.None);

        Assert.Equal("signature.already_collected", again.Error!.Code);
    }

    [Fact]
    public async Task Register_WithAPathFromAnotherFreight_IsRejected()
    {
        var (_, freightId) = await ScheduledFreightAsync();

        var result = await Service.RegisterAsync(
            CustomerId,
            new RegisterSignatureRequest(SignatureType.Customer, freightId, $"signatures/{Guid.NewGuid():N}/customer/x.png"),
            CancellationToken.None);

        Assert.Equal("signature.invalid_storage_path", result.Error!.Code);
    }

    [Fact]
    public async Task Register_WithoutTheUploadedFile_IsRejected()
    {
        var (_, freightId) = await ScheduledFreightAsync();
        var ticket = await Service.CreateUploadUrlAsync(
            CustomerId, new CreateSignatureUploadUrlRequest(SignatureType.Customer, freightId, "image/png"), CancellationToken.None);

        var result = await Service.RegisterAsync(
            CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, ticket.Value.StoragePath), CancellationToken.None);

        Assert.Equal("signature.file_not_uploaded", result.Error!.Code);
    }

    [Fact]
    public async Task Register_WithATooLargeFile_DeletesItAndFails()
    {
        var (_, freightId) = await ScheduledFreightAsync();
        var path = await UploadAsync(CustomerId, SignatureType.Customer, freightId, size: SignatureLimits.MaxSizeBytes + 1);

        var result = await Service.RegisterAsync(
            CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, path), CancellationToken.None);

        Assert.Equal("signature.file_too_large", result.Error!.Code);
        Assert.Contains(path, _storage.Deleted);
        Assert.Empty(Signatures.Items);
    }

    [Fact]
    public async Task CancelledDelivery_ReturnsConflict()
    {
        var (driverUserId, freightId) = await ScheduledFreightAsync();
        await Progress.CancelAsDriverAsync(driverUserId, Deliveries.Items.Single().Id, new FrotaOnline.Application.Deliveries.Dtos.CancelDeliveryRequest("Imprevisto"), CancellationToken.None);

        var result = await Service.CreateUploadUrlAsync(
            CustomerId, new CreateSignatureUploadUrlRequest(SignatureType.Customer, freightId, "image/png"), CancellationToken.None);

        Assert.Equal("signature.delivery_cancelled", result.Error!.Code);
    }

    [Fact]
    public async Task UploadUrl_WithoutStorage_ReturnsConflict()
    {
        var (_, freightId) = await ScheduledFreightAsync();
        _storage.IsConfigured = false;

        var result = await Service.CreateUploadUrlAsync(
            CustomerId, new CreateSignatureUploadUrlRequest(SignatureType.Customer, freightId, "image/png"), CancellationToken.None);

        Assert.Equal("storage.not_configured", result.Error!.Code);
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
        var path = await UploadAsync(CustomerId, SignatureType.Customer, freightId);
        await Service.RegisterAsync(CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, path), CancellationToken.None);
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
        var customerPath = await UploadAsync(CustomerId, SignatureType.Customer, freightId);
        await Service.RegisterAsync(CustomerId, new RegisterSignatureRequest(SignatureType.Customer, freightId, customerPath), CancellationToken.None);
        var driverPath = await UploadAsync(userId, SignatureType.Driver, freightId);
        await Service.RegisterAsync(userId, new RegisterSignatureRequest(SignatureType.Driver, freightId, driverPath), CancellationToken.None);
        await Progress.StartAsync(userId, deliveryId, CancellationToken.None);
        await Progress.CompleteStopAsync(userId, deliveryId, stops[0], new(null, null, null, null), CancellationToken.None);

        var result = await Progress.CompleteStopAsync(userId, deliveryId, stops[1], new(null, null, null, null), CancellationToken.None);

        Assert.Equal(DeliveryStatus.Delivered, result.Value.Status);
    }
}
