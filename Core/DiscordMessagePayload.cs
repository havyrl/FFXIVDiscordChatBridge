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

    /// <summary>Optional embeds to include in the message.</summary>
    public Embed[]? Embeds { get; init; }

    /// <summary>Optional message components (buttons) to attach.</summary>
    public MessageComponent? Components { get; init; }

    /// <summary>
    /// When set, components are automatically removed from the sent message after this duration.
    /// Useful for time-limited notifications like Duty Finder pops.
    /// </summary>
    public TimeSpan? ComponentTimeout { get; init; }

    /// <summary>
    /// When true, the message is sent as a DM to the admin user instead of via webhook to a guild channel.
    /// ChannelId and WebhookUrl are ignored for DM payloads.
    /// </summary>
    public bool IsDm { get; init; }

    /// <summary>Optional file attachment (e.g. a generated map image). Sent alongside the message content.</summary>
    public byte[]? Attachment { get; init; }

    /// <summary>Filename shown in Discord for <see cref="Attachment"/> (e.g. "map.jpg").</summary>
    public string? AttachmentFilename { get; init; }
}
