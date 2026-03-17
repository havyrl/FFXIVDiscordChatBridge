namespace FFXIVDiscordBridgePlugin.Core;

/// <summary>
/// Thread-safe bridge between a Discord /requestadmin interaction and the in-game approval popup.
/// </summary>
public sealed class AdminRequestService
{
    public sealed record PendingRequest(ulong UserId, string Username);

    private volatile PendingRequest? _pending;
    public PendingRequest? Pending => _pending;

    /// <summary>Fired (from a Discord thread) when a new request arrives.</summary>
    public event Action? RequestReceived;

    public void Submit(ulong userId, string username)
    {
        _pending = new PendingRequest(userId, username);
        RequestReceived?.Invoke();
    }

    public void Clear() => _pending = null;
}
