using FrotaOnline.Application.Abstractions.Privacy;
using FrotaOnline.Application.Addresses.Dtos;
using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Privacy;
using FrotaOnline.Application.Privacy.Dtos;
using FrotaOnline.Application.Privacy.Validators;
using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Auth;

namespace FrotaOnline.Tests.Privacy;

public class PrivacyServiceTests
{
    private const string Password = "Str0ngPass";

    private readonly FakeUserRepository _users = new();
    private readonly FakeClock _clock = new();
    private readonly FakeErasureStore _erasure = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly PrivacyService _sut;

    public PrivacyServiceTests()
    {
        _sut = new PrivacyService(
            _users, new FakePersonalDataQueries(_users), _erasure, _hasher, _clock, new DeleteAccountRequestValidator());
    }

    private User AddUser(UserRole role = UserRole.Customer)
    {
        var user = User.Create("Maria Souza", "maria@example.com", "+5511999999999", _hasher.Hash(Password), role);
        _users.Items.Add(user);
        return user;
    }

    [Fact]
    public async Task Delete_WithCorrectPassword_ErasesTheAccountAndListsWhatIsRetained()
    {
        var user = AddUser();

        var result = await _sut.DeleteAccountAsync(user.Id, new DeleteAccountRequest(Password, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { user.Id }, _erasure.Erased);
        Assert.NotEmpty(result.Value.RetainedData);
    }

    [Fact]
    public async Task Delete_WithWrongPassword_ErasesNothing()
    {
        var user = AddUser();

        var result = await _sut.DeleteAccountAsync(user.Id, new DeleteAccountRequest("wrong", true), CancellationToken.None);

        Assert.Equal("auth.invalid_credentials", result.Error!.Code);
        Assert.Empty(_erasure.Erased);
    }

    [Fact]
    public async Task Delete_WithoutConfirmation_IsAValidationError()
    {
        var user = AddUser();

        var result = await _sut.DeleteAccountAsync(user.Id, new DeleteAccountRequest(Password, false), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(_erasure.Erased);
    }

    [Fact]
    public async Task Delete_WhileADeliveryIsInProgress_ReturnsConflict()
    {
        var user = AddUser();
        _erasure.HasActiveDeliveries = true;

        var result = await _sut.DeleteAccountAsync(user.Id, new DeleteAccountRequest(Password, true), CancellationToken.None);

        Assert.Equal("privacy.active_deliveries", result.Error!.Code);
        Assert.Empty(_erasure.Erased);
    }

    [Fact]
    public async Task Delete_OfAnAdmin_IsForbidden()
    {
        var admin = AddUser(UserRole.Admin);

        var result = await _sut.DeleteAccountAsync(admin.Id, new DeleteAccountRequest(Password, true), CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Empty(_erasure.Erased);
    }

    [Fact]
    public async Task Delete_OfAnAlreadyAnonymizedAccount_ReturnsNotFound()
    {
        var user = AddUser();
        user.Anonymize(_clock.UtcNow);

        var result = await _sut.DeleteAccountAsync(user.Id, new DeleteAccountRequest(Password, true), CancellationToken.None);

        Assert.Equal("privacy.account_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Export_OfAnUnknownUser_ReturnsNotFound()
    {
        var result = await _sut.ExportAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("privacy.account_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Export_OfAKnownUser_ReturnsTheirData()
    {
        var user = AddUser();

        var result = await _sut.ExportAsync(user.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Email, result.Value.Account.Email);
    }

    // ---- domain rules behind the erasure

    [Fact]
    public void Anonymize_RemovesEveryIdentifierAndBlocksSignIn()
    {
        var user = AddUser();

        user.Anonymize(_clock.UtcNow);

        Assert.True(user.IsAnonymized);
        Assert.False(user.IsActive);
        Assert.DoesNotContain("maria", user.Email);
        Assert.EndsWith("@anonymized.invalid", user.Email);
        Assert.Equal("Removed user", user.FullName);
        Assert.False(_hasher.Verify(Password, user.PasswordHash));
        Assert.Throws<DomainException>(() => user.Anonymize(_clock.UtcNow));
    }

    [Fact]
    public void Anonymize_GivesDifferentUsersDifferentPlaceholders()
    {
        var first = AddUser();
        var second = User.Create("Joao", "joao@example.com", "+5511888888888", _hasher.Hash(Password), UserRole.Customer);

        first.Anonymize(_clock.UtcNow);
        second.Anonymize(_clock.UtcNow);

        Assert.NotEqual(first.Email, second.Email);
    }

    [Fact]
    public void DriverAnonymize_ErasesDocumentsAndStopsTheDriverFromOperating()
    {
        var profile = DriverProfile.Create(Guid.NewGuid(), "52998224725", "12345678901", "B");
        profile.SubmitForReview();
        profile.Approve(Guid.NewGuid(), _clock.UtcNow);

        profile.Anonymize();

        Assert.False(profile.CanOperate);
        Assert.DoesNotContain("529982", profile.TaxId);
        Assert.DoesNotContain("123456", profile.CnhNumber);
        Assert.True(profile.TaxId.Length <= 14 && profile.CnhNumber.Length <= 11);
    }

    [Fact]
    public void VehicleAndAddressAnonymize_RemoveThePlateAndTheStreet()
    {
        var vehicle = Vehicle.Create(Guid.NewGuid(), VehicleType.Fiorino, "ABC1D23", "Fiorino", 2020, 600);
        var address = Address.Create(Guid.NewGuid(), "Casa", "Rua A", "10", "ap 1", "Centro", "Sao Paulo", "SP", "01001000", -23.5, -46.6);

        vehicle.Anonymize();
        address.Anonymize();

        Assert.NotEqual("ABC1D23", vehicle.Plate);
        Assert.True(vehicle.Plate.Length <= 10);
        Assert.False(vehicle.IsActive);
        Assert.Equal("Removed", address.Street);
        Assert.Null(address.Latitude);
        Assert.Equal("Sao Paulo", address.City);
    }
}

internal sealed class FakeErasureStore : IAccountErasureStore
{
    public bool HasActiveDeliveries { get; set; }

    public List<Guid> Erased { get; } = new();

    public Task<bool> HasActiveDeliveriesAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(HasActiveDeliveries);

    public Task EraseAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        Erased.Add(userId);
        return Task.CompletedTask;
    }
}

internal sealed class FakePersonalDataQueries : IPersonalDataQueries
{
    private readonly FakeUserRepository _users;

    public FakePersonalDataQueries(FakeUserRepository users) => _users = users;

    public Task<PersonalDataExport?> GetAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var user = _users.Items.FirstOrDefault(u => u.Id == userId && !u.IsAnonymized);
        if (user is null)
        {
            return Task.FromResult<PersonalDataExport?>(null);
        }

        return Task.FromResult<PersonalDataExport?>(new PersonalDataExport(
            utcNow,
            new PersonalAccountData(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role, user.CreatedAt, user.LastLoginAt),
            null,
            null,
            Array.Empty<VehicleResponse>(),
            Array.Empty<AddressResponse>(),
            Array.Empty<MediaFileResponse>(),
            Array.Empty<PersonalConsentData>(),
            Array.Empty<PersonalFreightData>(),
            Array.Empty<PersonalProposalData>(),
            Array.Empty<PersonalDeliveryData>()));
    }
}
