namespace FFXIVDiscordBridgePlugin.Core;

/// <summary>
/// Abstraction for a source of FFXIV game events that produces Discord messages.
/// Implement this interface to bridge any game event (chat, duty pop, party invite, etc.)
/// to Discord. Implementations are auto-discovered via reflection at startup.
/// </summary>
public interface IGameEventSource : IDisposable
{
    /// <summary>
    /// Raised when the event source wants to send a message to Discord.
    /// Subscribe in PluginLifecycleManager; forward to WebhookSender.
    /// </summary>
    event Func<DiscordMessagePayload, Task> OnDiscordMessage;

    /// <summary>
    /// Called once after DI setup. Subscribe to Dalamud events here.
    /// </summary>
    void Initialize();
}
