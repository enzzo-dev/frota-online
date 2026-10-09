using FrotaOnline.Application.Abstractions.Auth;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Abstractions.Users;
using FrotaOnline.Application.Auth;
using FrotaOnline.Application.Auth.Dtos;
using FrotaOnline.Application.Auth.Validators;
using FrotaOnline.Application.Common;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Auth;

public class AuthServiceTests
{
    private const string ValidPassword = "Str0ngPass";

    private readonly FakeUserRepository _users = new();
    private readonly FakeAuthRepository _tokens = new();
    private readonly FakeClock _clock = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _users, _tokens, new FakeUnitOfWork(), new FakePasswordHasher(), new FakeTokenService(_clock), _clock,
            new RegisterRequestValidator(), new LoginRequestValidator(),
            new RefreshTokenRequestValidator(), new LogoutRequestValidator());
    }

    private static RegisterRequest NewRegistration(string email = "driver@example.com", UserRole role = UserRole.Driver) =>
        new("Test Driver", email, "+5511999999999", ValidPassword, role);

    [Fact]
    public async Task Register_CreatesUserAndReturnsTokens()
    {
        var result = await _sut.RegisterAsync(NewRegistration("Driver@Example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("driver@example.com", result.Value.User.Email);
        Assert.False(string.IsNullOrEmpty(result.Value.AccessToken));
        Assert.Single(_users.Items);
        Assert.Single(_tokens.Items);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsConflict()
    {
        await _sut.RegisterAsync(NewRegistration(), CancellationToken.None);

        var result = await _sut.RegisterAsync(NewRegistration(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public async Task Register_AsAdmin_IsRejectedByValidation()
    {
        var result = await _sut.RegisterAsync(NewRegistration(role: UserRole.Admin), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Register_WithWeakPassword_IsRejectedByValidation()
    {
        var request = NewRegistration() with { Password = "weak" };

        var result = await _sut.RegisterAsync(request, CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(_users.Items);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        await _sut.RegisterAsync(NewRegistration(), CancellationToken.None);

        var result = await _sut.LoginAsync(new LoginRequest("driver@example.com", "Wr0ngPassword"), CancellationToken.None);

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsSameErrorAsWrongPassword()
    {
        var result = await _sut.LoginAsync(new LoginRequest("ghost@example.com", ValidPassword), CancellationToken.None);

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsTokens()
    {
        await _sut.RegisterAsync(NewRegistration(), CancellationToken.None);

        var result = await _sut.LoginAsync(new LoginRequest("driver@example.com", ValidPassword), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _tokens.Items.Count);
    }

    [Fact]
    public async Task Refresh_RotatesTheToken()
    {
        var registered = (await _sut.RegisterAsync(NewRegistration(), CancellationToken.None)).Value;

        var refreshed = await _sut.RefreshAsync(new RefreshTokenRequest(registered.RefreshToken), CancellationToken.None);

        Assert.True(refreshed.IsSuccess);
        Assert.NotEqual(registered.RefreshToken, refreshed.Value.RefreshToken);
        Assert.Equal(1, _tokens.Items.Count(t => !t.IsRevoked));
    }

    [Fact]
    public async Task Refresh_ReusingARotatedToken_RevokesTheWholeSession()
    {
        var registered = (await _sut.RegisterAsync(NewRegistration(), CancellationToken.None)).Value;
        await _sut.RefreshAsync(new RefreshTokenRequest(registered.RefreshToken), CancellationToken.None);

        var reuse = await _sut.RefreshAsync(new RefreshTokenRequest(registered.RefreshToken), CancellationToken.None);

        Assert.Equal(AuthErrors.InvalidRefreshToken, reuse.Error);
        Assert.All(_tokens.Items, t => Assert.True(t.IsRevoked));
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_IsRejected()
    {
        var registered = (await _sut.RegisterAsync(NewRegistration(), CancellationToken.None)).Value;
        _clock.Advance(TimeSpan.FromDays(31));

        var result = await _sut.RefreshAsync(new RefreshTokenRequest(registered.RefreshToken), CancellationToken.None);

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task Logout_RevokesTheToken_AndIsIdempotent()
    {
        var registered = (await _sut.RegisterAsync(NewRegistration(), CancellationToken.None)).Value;

        var first = await _sut.LogoutAsync(new LogoutRequest(registered.RefreshToken), CancellationToken.None);
        var second = await _sut.LogoutAsync(new LogoutRequest("unknown-token"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.All(_tokens.Items, t => Assert.True(t.IsRevoked));
    }
}

internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Items { get; } = new();

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<User>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<User>>(Items.Where(u => ids.Contains(u.Id)).ToList());

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(u => u.Email == email.Trim().ToLowerInvariant()));

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(u => u.Email == email.Trim().ToLowerInvariant()));

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        Items.Add(user);
        return Task.CompletedTask;
    }
}

internal sealed class FakeAuthRepository : IAuthRepository
{
    public List<RefreshToken> Items { get; } = new();

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> GetActiveRefreshTokensByUserAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(
            Items.Where(t => t.UserId == userId && t.IsActive(utcNow)).ToList());

    public Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        Items.Add(refreshToken);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hashed:" + password;

    public bool Verify(string password, string passwordHash) => passwordHash == "hashed:" + password;
}

internal sealed class FakeClock : IDateTimeProvider
{
    public DateTime UtcNow { get; private set; } = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan by) => UtcNow += by;
}

internal sealed class FakeTokenService : ITokenService
{
    private readonly IDateTimeProvider _clock;
    private int _counter;

    public FakeTokenService(IDateTimeProvider clock) => _clock = clock;

    public AccessTokenResult CreateAccessToken(User user) =>
        new($"access-{user.Id}", _clock.UtcNow.AddMinutes(15));

    public RefreshTokenResult CreateRefreshToken()
    {
        var raw = $"refresh-{++_counter}";
        return new RefreshTokenResult(raw, HashRefreshToken(raw), _clock.UtcNow.AddDays(30));
    }

    public string HashRefreshToken(string rawValue) => "hash:" + rawValue;
}
