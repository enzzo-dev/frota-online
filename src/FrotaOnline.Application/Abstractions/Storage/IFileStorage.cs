namespace FrotaOnline.Application.Abstractions.Storage;

/// <summary>Where to send the file (PUT), and the headers the upload must carry exactly as given.</summary>
public sealed record UploadTicket(string Url, IReadOnlyDictionary<string, string> Headers, DateTime ExpiresAt);

/// <summary>What the storage really holds for a key, not what the client claimed.</summary>
public sealed record StoredObject(long SizeBytes, string ContentType);

/// <summary>
/// Private object storage. The API never touches file bytes: the app uploads straight to storage with a short-lived
/// signed URL, and the API only issues the URL, checks what arrived and deletes on request (LGPD).
/// </summary>
public interface IFileStorage
{
    /// <summary>False when no bucket is configured (local development): upload URLs are unavailable and uploads are not verified.</summary>
    bool IsConfigured { get; }

    /// <summary>A signed URL for one PUT of at most <paramref name="maxBytes"/> bytes with exactly this content type.</summary>
    Task<UploadTicket> CreateUploadAsync(
        string key, string contentType, long maxBytes, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>Null when nothing was uploaded under the key.</summary>
    Task<StoredObject?> GetInfoAsync(string key, CancellationToken cancellationToken);

    Task<string> CreateDownloadUrlAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>Deletes the objects. Must not throw: a storage hiccup must not undo the operation that asked for it.</summary>
    Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken);
}
