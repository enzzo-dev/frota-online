namespace FrotaOnline.Application.Abstractions.Auth;

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);
