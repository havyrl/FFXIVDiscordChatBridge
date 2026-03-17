using Dalamud.Game.Text;
using System.Collections.Concurrent;

namespace FFXIVDiscordBridgePlugin.Core;

/// <summary>
/// Correlates Discord slash commands (/say, /fc …) with the resulting FFXIV chat message
/// that bounces back through ChatEventSource.
/// ChatModule registers a pending confirmation; ChatEventSource resolves it when the
/// matching message arrives. If the message never arrives the timeout fires and the
/// ephemeral is kept (visible to the user as a hint something went wrong).
/// </summary>
public sealed class ChatConfirmationService
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pending =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Register an expectation for a chat message of <paramref name="type"/> with
    /// <paramref name="text"/> and return a Task that resolves to <c>true</c> when
    /// confirmed or <c>false</c> when <paramref name="timeout"/> expires.
    /// Must be called BEFORE the game command is executed to avoid a race.
    /// </summary>
    public Task<bool> WaitAsync(XivChatType type, string text, TimeSpan timeout)
    {
        var key = Key(type, text);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[key] = tcs;

        _ = Task.Delay(timeout).ContinueWith(__ =>
        {
            if (tcs.TrySetResult(false))
                _pending.TryRemove(key, out var unused);
        });

        return tcs.Task;
    }

    /// <summary>
    /// Called by ChatEventSource for every incoming chat message.
    /// Resolves any pending confirmation whose key matches.
    /// </summary>
    public void TryConfirm(XivChatType type, string text)
    {
        var key = Key(type, text);
        if (_pending.TryRemove(key, out var tcs))
            tcs.TrySetResult(true);
    }

    private static string Key(XivChatType type, string text) => $"{(int)type}\0{text}";
}
