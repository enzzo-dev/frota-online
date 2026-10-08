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

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
