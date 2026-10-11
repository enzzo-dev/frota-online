namespace FrotaOnline.Application.Notifications;

public interface INotificationDispatcher
{
    /// <summary>Sends one batch of pending pushes. Returns how many notifications were settled (sent or given up).</summary>
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken);
}
