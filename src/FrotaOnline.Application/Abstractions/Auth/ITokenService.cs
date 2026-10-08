using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Abstractions.Auth;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user);

    RefreshTokenResult CreateRefreshToken();

    string HashRefreshToken(string rawValue);
}
