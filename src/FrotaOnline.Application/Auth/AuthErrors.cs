using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Auth;

public static class AuthErrors
{
    public static readonly Error EmailAlreadyInUse =
        new("auth.email_in_use", "This e-mail is already registered.", ErrorType.Conflict);

    public static readonly Error InvalidCredentials =
        new("auth.invalid_credentials", "Invalid e-mail or password.", ErrorType.Unauthorized);

    public static readonly Error InvalidRefreshToken =
        new("auth.invalid_refresh_token", "The refresh token is invalid or has expired.", ErrorType.Unauthorized);

    public static readonly Error UserInactive =
        new("auth.user_inactive", "This account is disabled.", ErrorType.Forbidden);
}
