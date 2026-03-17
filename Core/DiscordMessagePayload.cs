using Discord;

namespace FFXIVDiscordBridgePlugin.Core;

/// <summary>
/// Represents a message to be sent from FFXIV to a Discord channel.
/// Built by IGameEventSource implementations, consumed by WebhookSender.
/// </summary>
public sealed class DiscordMessagePayload
{
    /// <summary>Target Discord channel ID.</summary>
    public required ulong ChannelId { get; init; }

    /// <summary>Webhook display name, e.g. "Firstname Lastname@World".</summary>
    public required string Username { get; init; }

    /// <summary>Message content, e.g. "[FC] Hello everyone".</summary>
    public required string Content { get; init; }

    /// <summary>Webhook URL to post through (from ChannelMapping.WebhookUrl).</summary>
    public required string WebhookUrl { get; init; }

    /// <summary>Optional avatar URL for the webhook post.</summary>
    public string? AvatarUrl { get; init; }

    /// <summary>Optional message components (buttons) to attach.</summary>
    public MessageComponent? Components { get; init; }
}
