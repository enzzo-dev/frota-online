using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Application.Abstractions.Terms;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Onboarding;

internal sealed class FakeTermRepository : ITermRepository
{
    public List<TermDocument> Items { get; } = new();

    public Task<TermDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(t => t.Id == id));

    public Task<IReadOnlyList<TermDocument>> ListActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TermDocument>>(Items.Where(t => t.IsActive).ToList());

    public Task<IReadOnlyList<TermDocument>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TermDocument>>(Items.Where(t => ids.Contains(t.Id)).ToList());

    public Task<bool> VersionExistsAsync(TermType type, string version, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(t => t.Type == type && t.Version == version));

    public Task AddAsync(TermDocument term, CancellationToken cancellationToken)
    {
        Items.Add(term);
        return Task.CompletedTask;
    }
}

internal sealed class FakeConsentRepository : IConsentRepository
{
    public List<UserConsent> Items { get; } = new();

    public Task<IReadOnlyList<UserConsent>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserConsent>>(Items.Where(c => c.UserId == userId).ToList());

    public Task AddAsync(UserConsent consent, CancellationToken cancellationToken)
    {
        Items.Add(consent);
        return Task.CompletedTask;
    }
}

internal sealed class FakeDriverRepository : IDriverRepository
{
    public List<DriverProfile> Items { get; } = new();

    public Task<DriverProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(d => d.Id == id));

    public Task<IReadOnlyList<DriverProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DriverProfile>>(Items.Where(d => ids.Contains(d.Id)).ToList());

    public Task<DriverProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(d => d.UserId == userId));

    public Task<IReadOnlyList<DriverProfile>> ListByStatusAsync(DriverStatus status, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DriverProfile>>(Items.Where(d => d.Status == status).ToList());

    public Task<bool> TaxIdInUseAsync(string taxId, Guid exceptUserId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(d => d.TaxId == taxId && d.UserId != exceptUserId));

    public Task<bool> CnhInUseAsync(string cnhNumber, Guid exceptUserId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(d => d.CnhNumber == cnhNumber && d.UserId != exceptUserId));

    public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken)
    {
        Items.Add(profile);
        return Task.CompletedTask;
    }
}

internal sealed class FakeVehicleRepository : IVehicleRepository
{
    public List<Vehicle> Items { get; } = new();

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(v => v.Id == id));

    public Task<IReadOnlyList<Vehicle>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Vehicle>>(Items.Where(v => ids.Contains(v.Id)).ToList());

    public Task<IReadOnlyList<Vehicle>> ListActiveByDriverAsync(Guid driverProfileId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Vehicle>>(Items.Where(v => v.DriverProfileId == driverProfileId && v.IsActive).ToList());

    public Task<bool> PlateInUseAsync(string plate, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(v => v.Plate == plate));

    public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        Items.Add(vehicle);
        return Task.CompletedTask;
    }
}

internal sealed class FakeMediaFileRepository : IMediaFileRepository
{
    public List<MediaFile> Items { get; } = new();

    public Task<IReadOnlyList<MediaFile>> ListByOwnerAsync(
        Guid ownerUserId, IReadOnlyCollection<MediaKind> kinds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MediaFile>>(Items.Where(m => m.OwnerUserId == ownerUserId && kinds.Contains(m.Kind)).ToList());

    public Task<bool> StorageKeyExistsAsync(string storageKey, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(m => m.StorageKey == storageKey));

    public Task AddAsync(MediaFile file, CancellationToken cancellationToken)
    {
        Items.Add(file);
        return Task.CompletedTask;
    }
}

internal sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        Sent.Add((toAddress, subject, body));
        return Task.CompletedTask;
    }
}
