namespace FrotaOnline.Application.Abstractions.Auth;

/// <summary>RawValue is returned to the client once; only Hash is stored.</summary>
public sealed record RefreshTokenResult(string RawValue, string Hash, DateTime ExpiresAt);
