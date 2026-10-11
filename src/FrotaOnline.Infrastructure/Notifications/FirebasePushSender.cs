using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using FrotaOnline.Application.Abstractions.Notifications;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;

namespace FrotaOnline.Infrastructure.Notifications;

/// <summary>
/// Sends pushes through Firebase Cloud Messaging (free). Credentials come from Application Default Credentials:
/// on Cloud Run / GKE the runtime service account is used (it needs the Firebase Cloud Messaging API Admin role);
/// locally set GOOGLE_APPLICATION_CREDENTIALS to a service-account JSON file. FCM is used for Android, iOS
/// (through APNs) and Web alike.
/// </summary>
internal sealed class FirebasePushSender : IPushSender
{
    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FirebasePushSender> _logger;

    public FirebasePushSender(ILogger<FirebasePushSender> logger)
    {
        _logger = logger;
        var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.GetApplicationDefault()
        });
        _messaging = FirebaseMessaging.GetMessaging(app);
    }

    public async Task<PushResult> SendAsync(
        PushMessage message, IReadOnlyList<PushDevice> devices, CancellationToken cancellationToken)
    {
        var multicast = new MulticastMessage
        {
            Tokens = devices.Select(d => d.Token).ToList(),
            Notification = new Notification { Title = message.Title, Body = message.Body },
            Data = message.Data.ToDictionary(pair => pair.Key, pair => pair.Value)
        };

        try
        {
            var batch = await _messaging.SendEachForMulticastAsync(multicast, cancellationToken);

            var invalid = new List<string>();
            var transientFailure = false;
            for (var i = 0; i < batch.Responses.Count; i++)
            {
                var response = batch.Responses[i];
                if (response.IsSuccess)
                {
                    continue;
                }

                switch (response.Exception?.MessagingErrorCode)
                {
                    case MessagingErrorCode.Unregistered:
                    case MessagingErrorCode.InvalidArgument:
                    case MessagingErrorCode.SenderIdMismatch:
                        invalid.Add(multicast.Tokens[i]);
                        break;
                    default:
                        transientFailure = true;
                        break;
                }
            }

            // At least one device got it, or all that failed are dead tokens: nothing to retry.
            var anySent = batch.SuccessCount > 0;
            return new PushResult(anySent || !transientFailure, invalid);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Firebase push failed. It will be retried.");
            return new PushResult(false, Array.Empty<string>());
        }
    }
}
