using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Auth.Dtos;

public sealed record RegisterRequest(string FullName, string Email, string PhoneNumber, string Password, UserRole Role);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record UserSummary(Guid Id, string FullName, string Email, UserRole Role);

public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserSummary User);
