namespace FrotaOnline.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary><c>Gcs</c> (Google Cloud Storage) or <c>None</c> (default: no storage, local development).</summary>
    public string Provider { get; init; } = "None";

    /// <summary>Private bucket name. Required with <c>Gcs</c>.</summary>
    public string Bucket { get; init; } = string.Empty;
}
