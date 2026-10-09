using FluentValidation;
using FluentValidation.Results;
using FrotaOnline.Application.Abstractions.Auth;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Abstractions.Users;
using FrotaOnline.Application.Auth.Dtos;
using FrotaOnline.Application.Common;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IAuthRepository _authRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshTokenRequest> _refreshValidator;
    private readonly IValidator<LogoutRequest> _logoutValidator;

    public AuthService(
        IUserRepository users,
        IAuthRepository authRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IDateTimeProvider clock,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshTokenRequest> refreshValidator,
        IValidator<LogoutRequest> logoutValidator)
    {
        _users = users;
        _authRepository = authRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _clock = clock;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
        _logoutValidator = logoutValidator;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var validation = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AuthResponse>(ToError(validation));
        }

        if (await _users.EmailExistsAsync(request.Email, cancellationToken))
        {
            return Result.Failure<AuthResponse>(AuthErrors.EmailAlreadyInUse);
        }

        var user = User.Create(
            request.FullName,
            request.Email,
            request.PhoneNumber,
            _passwordHasher.Hash(request.Password),
            request.Role);

        user.RegisterLogin(_clock.UtcNow);
        await _users.AddAsync(user, cancellationToken);

        var response = await IssueTokensAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(response);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AuthResponse>(ToError(validation));
        }

        var user = await _users.GetByEmailAsync(request.Email, cancellationToken);

        // Same error for "unknown e-mail" and "wrong password" to avoid user enumeration.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthResponse>(AuthErrors.UserInactive);
        }

        user.RegisterLogin(_clock.UtcNow);

        var response = await IssueTokensAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(response);
    }

    public async Task<Result<AuthResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var validation = await _refreshValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AuthResponse>(ToError(validation));
        }

        var now = _clock.UtcNow;
        var stored = await _authRepository.GetRefreshTokenByHashAsync(_tokenService.HashRefreshToken(request.RefreshToken), cancellationToken);

        if (stored is null)
        {
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);
        }

        if (stored.IsRevoked)
        {
            // A rotated token was presented again: assume theft and revoke the whole session family.
            await RevokeAllAsync(stored.UserId, now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);
        }

        if (stored.IsExpired(now))
        {
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);
        }

        var user = await _users.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);
        }

        var response = await IssueTokensAsync(user, cancellationToken);
        stored.Revoke(now, _tokenService.HashRefreshToken(response.RefreshToken));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(response);
    }

    public async Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var validation = await _logoutValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(ToError(validation));
        }

        var stored = await _authRepository.GetRefreshTokenByHashAsync(_tokenService.HashRefreshToken(request.RefreshToken), cancellationToken);
        if (stored is not null)
        {
            stored.Revoke(_clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Logout is idempotent: unknown tokens are not an error.
        return Result.Success();
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var access = _tokenService.CreateAccessToken(user);
        var refresh = _tokenService.CreateRefreshToken();

        await _authRepository.AddRefreshTokenAsync(RefreshToken.Create(user.Id, refresh.Hash, refresh.ExpiresAt), cancellationToken);

        return new AuthResponse(
            access.Token,
            access.ExpiresAt,
            refresh.RawValue,
            refresh.ExpiresAt,
            new UserSummary(user.Id, user.FullName, user.Email, user.Role));
    }

    private async Task RevokeAllAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        var active = await _authRepository.GetActiveRefreshTokensByUserAsync(userId, now, cancellationToken);
        foreach (var token in active)
        {
            token.Revoke(now);
        }
    }

    private static Error ToError(ValidationResult validation) => ValidationMapper.ToError(validation);
}
