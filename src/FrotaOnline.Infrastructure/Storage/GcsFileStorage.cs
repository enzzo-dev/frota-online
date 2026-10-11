using FrotaOnline.Application.Abstractions.Storage;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FrotaOnline.Infrastructure.Storage;

/// <summary>
/// Google Cloud Storage with V4 signed URLs. Credentials come from Application Default Credentials. On Cloud Run the
/// runtime service account signs through the IAM Credentials API, so it needs the "Service Account Token Creator"
/// role on itself and "Storage Object Admin" on the bucket; no key file is kept anywhere. Locally, point
/// GOOGLE_APPLICATION_CREDENTIALS to a service account key file.
/// The bucket must be private and should have a CORS rule allowing PUT from the web origins, if any.
/// </summary>
internal sealed class GcsFileStorage : IFileStorage
{
    private const string SizeRangeHeader = "x-goog-content-length-range";

    private readonly string _bucket;
    private readonly StorageClient _client;
    private readonly UrlSigner _signer;
    private readonly ILogger<GcsFileStorage> _logger;

    public GcsFileStorage(IOptions<StorageOptions> options, ILogger<GcsFileStorage> logger)
    {
        _logger = logger;
        _bucket = options.Value.Bucket;
        var credential = GoogleCredential.GetApplicationDefault();
        _client = StorageClient.Create(credential);
        _signer = UrlSigner.FromCredential(credential);
    }

    public bool IsConfigured => true;

    public async Task<UploadTicket> CreateUploadAsync(
        string key, string contentType, long maxBytes, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        // Both headers are part of the signature: the client must send them as given, so it cannot upload another
        // type or a file larger than allowed.
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = contentType,
            [SizeRangeHeader] = $"0,{maxBytes}"
        };

        var template = UrlSigner.RequestTemplate
            .FromBucket(_bucket)
            .WithObjectName(key)
            .WithHttpMethod(HttpMethod.Put)
            .WithContentHeaders(new Dictionary<string, IEnumerable<string>> { ["Content-Type"] = new[] { contentType } })
            .WithRequestHeaders(new Dictionary<string, IEnumerable<string>> { [SizeRangeHeader] = new[] { $"0,{maxBytes}" } });

        var url = await _signer.SignAsync(template, UrlSigner.Options.FromDuration(lifetime), cancellationToken);
        return new UploadTicket(url, headers, DateTime.UtcNow + lifetime);
    }

    public async Task<StoredObject?> GetInfoAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await _client.GetObjectAsync(_bucket, key, cancellationToken: cancellationToken);
            return new StoredObject((long)(metadata.Size ?? 0), metadata.ContentType ?? string.Empty);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<string> CreateDownloadUrlAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var template = UrlSigner.RequestTemplate
            .FromBucket(_bucket)
            .WithObjectName(key)
            .WithHttpMethod(HttpMethod.Get);
        return _signer.SignAsync(template, UrlSigner.Options.FromDuration(lifetime), cancellationToken);
    }

    public async Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        foreach (var key in keys)
        {
            try
            {
                await _client.DeleteObjectAsync(_bucket, key, cancellationToken: cancellationToken);
            }
            catch (GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Already gone.
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Could not delete storage object {Key}. Remove it by hand or with a lifecycle rule.", key);
            }
        }
    }
}
