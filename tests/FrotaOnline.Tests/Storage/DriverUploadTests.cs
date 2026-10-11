using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Application.Drivers.Validators;
using FrotaOnline.Application.Abstractions.Storage;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Auth;
using FrotaOnline.Tests.Notifications;
using FrotaOnline.Tests.Onboarding;

namespace FrotaOnline.Tests.Storage;

public class DriverUploadTests
{
    private readonly FakeDriverRepository _drivers = new();
    private readonly FakeMediaFileRepository _media = new();
    private readonly FakeFileStorage _storage = new() { IsConfigured = true };
    private readonly DriverDocumentService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public DriverUploadTests()
    {
        _drivers.Items.Add(DriverProfile.Create(_userId, "52998224725", "12345678901", "B"));
        _sut = new DriverDocumentService(
            _drivers, _media, _storage, new FakeUnitOfWork(),
            new RegisterMediaRequestValidator(), new CreateUploadUrlRequestValidator());
    }

    [Fact]
    public async Task UploadUrl_IsIssuedUnderTheDriversFolder_WithTheExpectedHeaders()
    {
        var result = await _sut.CreateUploadUrlAsync(
            _userId, new CreateUploadUrlRequest(MediaKind.DriverDocument, "application/pdf"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.StartsWith($"drivers/{_userId}/", result.Value.StorageKey);
        Assert.EndsWith(".pdf", result.Value.StorageKey);
        Assert.Equal("PUT", result.Value.Method);
        Assert.Equal("application/pdf", result.Value.Headers["Content-Type"]);
        Assert.Equal(RegisterMediaRequestValidator.MaxSizeBytes, result.Value.MaxBytes);
    }

    [Fact]
    public async Task UploadUrl_TwoCalls_GiveDifferentKeys()
    {
        var request = new CreateUploadUrlRequest(MediaKind.VehiclePhoto, "image/jpeg");

        var first = await _sut.CreateUploadUrlAsync(_userId, request, CancellationToken.None);
        var second = await _sut.CreateUploadUrlAsync(_userId, request, CancellationToken.None);

        Assert.NotEqual(first.Value.StorageKey, second.Value.StorageKey);
    }

    [Fact]
    public async Task UploadUrl_WithoutStorage_ReturnsConflict()
    {
        _storage.IsConfigured = false;

        var result = await _sut.CreateUploadUrlAsync(
            _userId, new CreateUploadUrlRequest(MediaKind.DriverDocument, "image/png"), CancellationToken.None);

        Assert.Equal("storage.not_configured", result.Error!.Code);
    }

    [Fact]
    public async Task UploadUrl_ForAnUnsupportedType_ReturnsValidationError()
    {
        var result = await _sut.CreateUploadUrlAsync(
            _userId, new CreateUploadUrlRequest(MediaKind.DriverDocument, "application/zip"), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task UploadUrl_WithoutAProfile_ReturnsNotFound()
    {
        var result = await _sut.CreateUploadUrlAsync(
            Guid.NewGuid(), new CreateUploadUrlRequest(MediaKind.DriverDocument, "image/png"), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Register_TrustsTheStoredObject_NotTheRequest()
    {
        var key = $"drivers/{_userId}/abc.png";
        _storage.Objects[key] = new StoredObject(5000, "image/png");

        var result = await _sut.RegisterAsync(
            _userId, new RegisterMediaRequest(MediaKind.DriverDocument, key, "image/jpeg", 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(5000, result.Value.SizeBytes);
        Assert.Equal("image/png", result.Value.ContentType);
    }

    [Fact]
    public async Task Register_WhenNothingWasUploaded_ReturnsAValidationError()
    {
        var result = await _sut.RegisterAsync(
            _userId, new RegisterMediaRequest(MediaKind.DriverDocument, $"drivers/{_userId}/none.png", "image/png", 100),
            CancellationToken.None);

        Assert.Equal("driver.file_not_uploaded", result.Error!.Code);
        Assert.Empty(_media.Items);
    }

    [Fact]
    public async Task Register_OfAnOversizedUpload_DeletesItAndFails()
    {
        var key = $"drivers/{_userId}/big.pdf";
        _storage.Objects[key] = new StoredObject(RegisterMediaRequestValidator.MaxSizeBytes + 1, "application/pdf");

        var result = await _sut.RegisterAsync(
            _userId, new RegisterMediaRequest(MediaKind.DriverDocument, key, "application/pdf", 100), CancellationToken.None);

        Assert.Equal("driver.file_too_large", result.Error!.Code);
        Assert.Contains(key, _storage.Deleted);
    }

    [Fact]
    public async Task Register_OfAForbiddenStoredType_DeletesItAndFails()
    {
        var key = $"drivers/{_userId}/x.png";
        _storage.Objects[key] = new StoredObject(100, "application/x-msdownload");

        var result = await _sut.RegisterAsync(
            _userId, new RegisterMediaRequest(MediaKind.DriverDocument, key, "image/png", 100), CancellationToken.None);

        Assert.Equal("driver.file_type_not_allowed", result.Error!.Code);
        Assert.Contains(key, _storage.Deleted);
    }

    [Fact]
    public async Task Register_WithoutStorage_KeepsTheOldBehaviour()
    {
        _storage.IsConfigured = false;
        var key = $"drivers/{_userId}/cnh.jpg";

        var result = await _sut.RegisterAsync(
            _userId, new RegisterMediaRequest(MediaKind.DriverDocument, key, "image/jpeg", 2048), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2048, result.Value.SizeBytes);
    }
}
