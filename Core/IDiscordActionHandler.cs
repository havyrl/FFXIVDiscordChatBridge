using Discord.WebSocket;

namespace FFXIVDiscordBridgePlugin.Core;

/// <summary>
/// Abstraction for handling Discord button clicks and modal submissions
/// that trigger actions inside FFXIV.
/// Implementations are auto-discovered via reflection at startup.
/// Custom IDs follow the schema: bridge:&lt;domain&gt;:&lt;action&gt;:&lt;encodedPayload&gt;
/// </summary>
public interface IDiscordActionHandler
{
    /// <summary>
    /// Returns true if this handler is responsible for the given Discord custom ID.
    /// Called by BotEventDispatcher to route interactions.
    /// </summary>
    bool CanHandle(string customId);

    /// <summary>
    /// Handles the interaction. Must use IFramework.RunOnFrameworkThread()
    /// for any Dalamud or game API calls.
    /// </summary>
    Task HandleAsync(SocketInteraction interaction);
}
