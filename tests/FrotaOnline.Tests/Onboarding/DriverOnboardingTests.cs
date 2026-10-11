using FrotaOnline.Application.Common;
using FrotaOnline.Application.Drivers;
using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Application.Drivers.Validators;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Auth;
using FrotaOnline.Tests.Notifications;

namespace FrotaOnline.Tests.Onboarding;

public class DriverOnboardingTests
{
    private const string ValidCpf = "529.982.247-25";
    private const string ValidAlphanumericCnpj = "12.ABC.345/01DE-35";

    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeDriverRepository _drivers = new();
    private readonly FakeVehicleRepository _vehicles = new();
    private readonly FrotaOnline.Tests.Payments.FakeSubscriptionRepository _subscriptions = new();
    private readonly FakeMediaFileRepository _media = new();
    private readonly FakeFileStorage _storage = new();
    private readonly FakeTermRepository _terms = new();
    private readonly FakeConsentRepository _consents = new();
    private readonly FakeClock _clock = new();
    private readonly DriverProfileService _profile;
    private readonly VehicleService _vehicleService;
    private readonly DriverDocumentService _documents;

    public DriverOnboardingTests()
    {
        var unitOfWork = new FakeUnitOfWork();
        _profile = new DriverProfileService(
            _drivers, _vehicles, _media, _terms, _consents, unitOfWork, new UpsertDriverProfileRequestValidator());
        _vehicleService = new VehicleService(_drivers, _vehicles, new FrotaOnline.Application.Payments.SubscriptionPolicy(_subscriptions, new FrotaOnline.Tests.Payments.FakeSubscriptionSettings(), new FrotaOnline.Tests.Auth.FakeClock()), unitOfWork, new RegisterVehicleRequestValidator());
        _documents = new DriverDocumentService(_drivers, _media, _storage, unitOfWork, new RegisterMediaRequestValidator(), new CreateUploadUrlRequestValidator());
    }

    private static UpsertDriverProfileRequest ValidProfile(string taxId = ValidCpf, string cnh = "12345678901") =>
        new(taxId, cnh, "b");

    private static RegisterVehicleRequest ValidVehicle(string plate = "ABC-1D23") =>
        new(VehicleType.Fiorino, plate, "Fiat Fiorino", 2020, 650);

    private Task<FrotaOnline.Application.Common.Result<DriverProfileResponse>> CreateProfile() =>
        _profile.UpsertAsync(_userId, ValidProfile(), CancellationToken.None);

    private async Task MakeReadyToSubmit()
    {
        await CreateProfile();
        await _vehicleService.RegisterAsync(_userId, ValidVehicle(), CancellationToken.None);
        await _documents.RegisterAsync(_userId,
            new RegisterMediaRequest(MediaKind.DriverDocument, $"drivers/{_userId}/cnh.jpg", "image/jpeg", 2048), CancellationToken.None);
    }

    [Fact]
    public async Task Upsert_CreatesProfileWithNormalizedDocuments()
    {
        var result = await CreateProfile();

        Assert.True(result.IsSuccess);
        Assert.Equal("52998224725", result.Value.TaxId);
        Assert.Equal("B", result.Value.CnhCategory);
        Assert.Equal(DriverStatus.Draft, result.Value.Status);
    }

    [Fact]
    public async Task Upsert_WithInvalidCpf_IsRejected()
    {
        var result = await _profile.UpsertAsync(_userId, ValidProfile(taxId: "111.111.111-11"), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(_drivers.Items);
    }

    [Fact]
    public async Task Upsert_WithTaxIdOfAnotherDriver_ReturnsConflict()
    {
        await CreateProfile();

        var result = await _profile.UpsertAsync(Guid.NewGuid(), ValidProfile(cnh: "10987654321"), CancellationToken.None);

        Assert.Equal("driver.tax_id_in_use", result.Error!.Code);
    }

    [Fact]
    public async Task Upsert_AcceptsAlphanumericCnpj_AndNormalizesIt()
    {
        var result = await _profile.UpsertAsync(_userId, ValidProfile(taxId: ValidAlphanumericCnpj), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("12ABC34501DE35", result.Value.TaxId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12.ABC.345/01DE-36")]
    [InlineData("12ABC34501DE3")]
    public async Task Upsert_WithMissingOrInvalidTaxId_ReturnsValidationErrorAndSavesNothing(string taxId)
    {
        var result = await _profile.UpsertAsync(_userId, ValidProfile(taxId: taxId), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains("TaxId", result.Error.ValidationErrors!.Keys);
        Assert.Empty(_drivers.Items);
    }

    [Fact]
    public async Task Upsert_UpdatesExistingDraft()
    {
        await CreateProfile();

        var result = await _profile.UpsertAsync(_userId, ValidProfile(cnh: "10987654321"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_drivers.Items);
        Assert.Equal("10987654321", result.Value.CnhNumber);
    }

    [Fact]
    public async Task Submit_WithoutVehicleAndDocument_ListsWhatIsMissing()
    {
        await CreateProfile();

        var result = await _profile.SubmitForReviewAsync(_userId, CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains("vehicles", result.Error.ValidationErrors!.Keys);
        Assert.Contains("documents", result.Error.ValidationErrors.Keys);
        Assert.DoesNotContain("terms", result.Error.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Submit_WithUnacceptedActiveTerm_ListsTerms()
    {
        await MakeReadyToSubmit();
        _terms.Items.Add(TermDocument.Create(TermType.TermsOfUse, "1.0", "Terms", "https://example.com/t", _clock.UtcNow));

        var result = await _profile.SubmitForReviewAsync(_userId, CancellationToken.None);

        Assert.Contains("terms", result.Error!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Submit_WhenReady_MovesToPendingReview_AndLocksTheProfile()
    {
        await MakeReadyToSubmit();

        var submit = await _profile.SubmitForReviewAsync(_userId, CancellationToken.None);
        var edit = await _profile.UpsertAsync(_userId, ValidProfile(cnh: "10987654321"), CancellationToken.None);

        Assert.Equal(DriverStatus.PendingReview, submit.Value.Status);
        Assert.Equal("driver.profile_locked", edit.Error!.Code);
    }

    [Fact]
    public async Task Submit_Twice_ReturnsConflict()
    {
        await MakeReadyToSubmit();
        await _profile.SubmitForReviewAsync(_userId, CancellationToken.None);

        var result = await _profile.SubmitForReviewAsync(_userId, CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public async Task Vehicle_WithoutProfile_ReturnsNotFound()
    {
        var result = await _vehicleService.RegisterAsync(_userId, ValidVehicle(), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Vehicle_NormalizesPlate_AndRejectsDuplicates()
    {
        await CreateProfile();

        var first = await _vehicleService.RegisterAsync(_userId, ValidVehicle("abc-1d23"), CancellationToken.None);
        var second = await _vehicleService.RegisterAsync(_userId, ValidVehicle("ABC1D23"), CancellationToken.None);

        Assert.Equal("ABC1D23", first.Value.Plate);
        Assert.Equal("driver.plate_in_use", second.Error!.Code);
    }

    [Fact]
    public async Task Vehicle_WithInvalidPlate_IsRejected()
    {
        await CreateProfile();

        var result = await _vehicleService.RegisterAsync(_userId, ValidVehicle("12-ABCD"), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Vehicle_OfAnotherDriver_CannotBeDeactivated()
    {
        await CreateProfile();
        var vehicle = await _vehicleService.RegisterAsync(_userId, ValidVehicle(), CancellationToken.None);
        var intruder = Guid.NewGuid();
        await _profile.UpsertAsync(intruder, new UpsertDriverProfileRequest("168.995.350-09", "10987654321", "C"), CancellationToken.None);

        var result = await _vehicleService.DeactivateAsync(intruder, vehicle.Value.Id, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Vehicle_LastActiveOfApprovedDriver_CannotBeDeactivated()
    {
        await MakeReadyToSubmit();
        await _profile.SubmitForReviewAsync(_userId, CancellationToken.None);
        _drivers.Items.Single().Approve(Guid.NewGuid(), _clock.UtcNow);
        var vehicle = _vehicles.Items.Single();

        var result = await _vehicleService.DeactivateAsync(_userId, vehicle.Id, CancellationToken.None);

        Assert.Equal("driver.last_vehicle", result.Error!.Code);
    }

    [Fact]
    public async Task Document_WithKeyOutsideDriverFolder_IsRejected()
    {
        await CreateProfile();

        var result = await _documents.RegisterAsync(_userId,
            new RegisterMediaRequest(MediaKind.DriverDocument, $"drivers/{Guid.NewGuid()}/cnh.jpg", "image/jpeg", 100), CancellationToken.None);

        Assert.Equal("driver.invalid_storage_key", result.Error!.Code);
        Assert.Empty(_media.Items);
    }

    [Fact]
    public async Task Document_WithDisallowedContentType_IsRejected()
    {
        await CreateProfile();

        var result = await _documents.RegisterAsync(_userId,
            new RegisterMediaRequest(MediaKind.DriverDocument, $"drivers/{_userId}/x.exe", "application/x-msdownload", 100), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public void Cpf_ValidatesCheckDigits()
    {
        Assert.True(BrazilianDocuments.IsValidCpf("529.982.247-25"));
        Assert.False(BrazilianDocuments.IsValidCpf("529.982.247-24"));
        Assert.False(BrazilianDocuments.IsValidCpf("00000000000"));
        Assert.False(BrazilianDocuments.IsValidCpf("123"));
    }

    [Theory]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("12.ABC.345/01DE-35", true)]
    [InlineData("12ABC34501DE35", true)]
    [InlineData("12abc34501de35", true)]
    [InlineData("12.ABC.345/01DE-36", false)]
    [InlineData("11.222.333/0001-82", false)]
    [InlineData("00.000.000/0000-00", false)]
    [InlineData("12ABC34501DEAB", false)]
    [InlineData("529.982.247-25", true)]
    [InlineData("ABCDEFGHIJK", false)]
    public void TaxId_AcceptsCpfAndNumericOrAlphanumericCnpj(string value, bool expected) =>
        Assert.Equal(expected, BrazilianDocuments.IsValidTaxId(value));

    [Theory]
    [InlineData("ABC-1234", true)]
    [InlineData("abc1d23", true)]
    [InlineData("AB-12345", false)]
    [InlineData("ABCD123", false)]
    public void Plate_AcceptsOldAndMercosulFormats(string plate, bool expected) =>
        Assert.Equal(expected, BrazilianDocuments.IsValidPlate(plate));
}
