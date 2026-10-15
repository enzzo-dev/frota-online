using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

public sealed class User : BaseEntity, IAggregateRoot
{
    private User()
    {
    }

    public string FullName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTime? LastLoginAt { get; private set; }

    /// <summary>When the account was anonymized after an erasure request (LGPD art. 18, VI). Null while the account is live.</summary>
    public DateTime? AnonymizedAt { get; private set; }

    public bool IsAnonymized => AnonymizedAt.HasValue;

    public static User Create(string fullName, string email, string phoneNumber, string passwordHash, UserRole role)
    {
        return new User
        {
            FullName = Guard.NotNullOrWhiteSpace(fullName, nameof(fullName)),
            Email = Guard.NotNullOrWhiteSpace(email, nameof(email)).ToLowerInvariant(),
            PhoneNumber = Guard.NotNullOrWhiteSpace(phoneNumber, nameof(phoneNumber)),
            PasswordHash = Guard.NotNullOrWhiteSpace(passwordHash, nameof(passwordHash)),
            Role = role
        };
    }

    public void RegisterLogin(DateTime utcNow) => LastLoginAt = utcNow;

    public void ChangePasswordHash(string passwordHash) =>
        PasswordHash = Guard.NotNullOrWhiteSpace(passwordHash, nameof(passwordHash));

    /// <summary>
    /// Irreversibly removes the personal data. The row stays so freights, deliveries and ledgers that point to it
    /// keep their integrity, but it no longer identifies anyone and nobody can sign in to it.
    /// </summary>
    public void Anonymize(DateTime utcNow)
    {
        if (IsAnonymized)
        {
            throw new DomainException("Esta conta já foi anonimizada.");
        }

        FullName = "Usuário removido";
        Email = $"deleted-{Id:N}@anonymized.invalid";
        PhoneNumber = "0";
        PasswordHash = "!"; // not a valid hash, so no password can ever match
        IsActive = false;
        AnonymizedAt = utcNow;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
