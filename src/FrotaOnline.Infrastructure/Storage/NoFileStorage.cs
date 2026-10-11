using FrotaOnline.Application.Abstractions.Storage;

namespace FrotaOnline.Infrastructure.Storage;

/// <summary>
/// Used when <c>Storage:Provider</c> is not <c>Gcs</c>. Nothing can be uploaded through the API, and registering a file
/// keeps trusting the client's description of it (the old behaviour), which is fine for local development only.
/// </summary>
internal sealed class NoFileStorage : IFileStorage
{
    public bool IsConfigured => false;

    public Task<UploadTicket> CreateUploadAsync(
        string key, string contentType, long maxBytes, TimeSpan lifetime, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("File storage is not configured.");

    public Task<StoredObject?> GetInfoAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult<StoredObject?>(null);

    public Task<string> CreateDownloadUrlAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("File storage is not configured.");

    public Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken) => Task.CompletedTask;
}
