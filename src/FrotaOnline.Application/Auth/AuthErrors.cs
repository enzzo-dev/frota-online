using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Auth;

public static class AuthErrors
{
    public static readonly Error EmailAlreadyInUse =
        new("auth.email_in_use", "Este e-mail já está cadastrado.", ErrorType.Conflict);

    public static readonly Error InvalidCredentials =
        new("auth.invalid_credentials", "E-mail ou senha inválidos.", ErrorType.Unauthorized);

    public static readonly Error InvalidRefreshToken =
        new("auth.invalid_refresh_token", "O token de renovação é inválido ou expirou.", ErrorType.Unauthorized);

    public static readonly Error UserInactive =
        new("auth.user_inactive", "Esta conta está desativada.", ErrorType.Forbidden);
}
