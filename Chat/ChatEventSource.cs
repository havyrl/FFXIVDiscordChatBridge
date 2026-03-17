using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Discord;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Chat;

/// <summary>
/// IGameEventSource implementation for FFXIV chat messages.
/// Subscribes to IChatGui.ChatMessage, matches each message against the configured
/// ChannelMappings, and raises OnDiscordMessage for each matching mapping.
///
/// Special handling:
/// - TellIncoming: adds a Reply button (bridge:tell:reply:&lt;senderName&gt;) if the sender
///   is linked to a Discord user, and tracks the sender for /tell autocomplete.
/// - All other types: plain webhook message with [slug] prefix.
/// - FFXIV private-use Unicode characters are replaced via SpecialCharsHandler.
/// - Avatar URLs are resolved asynchronously via CharacterAvatarService.
/// </summary>
public sealed class ChatEventSource : IGameEventSource
{
    public event Func<DiscordMessagePayload, Task>? OnDiscordMessage;

    private readonly IChatGui _chatGui;
    private readonly IClientState _clientState;
    private readonly IConfigStore _configStore;
    private readonly IPluginLog _log;
    private readonly SpecialCharsHandler _specialChars;
    private readonly CharacterAvatarService _avatarService;

    public ChatEventSource(IChatGui chatGui, IClientState clientState,
                           IConfigStore configStore, IPluginLog log,
                           SpecialCharsHandler specialChars, CharacterAvatarService avatarService)
    {
        _chatGui = chatGui;
        _clientState = clientState;
        _configStore = configStore;
        _log = log;
        _specialChars = specialChars;
        _avatarService = avatarService;
    }

    public void Initialize() => _chatGui.ChatMessage += OnChatMessage;

    // ── IChatGui handler ───────────────────────────────────────────────────

    private void OnChatMessage(XivChatType type, int timestamp,
                               ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (OnDiscordMessage is null) return;

        var normalizedType = (XivChatType)((int)type & 0x7F);
        var config = _configStore.Load();

        var matchingMappings = config.ChannelMappings
            .Where(m => m.InboundChatTypes.Contains(normalizedType))
            .ToList();

        if (matchingMappings.Count == 0) return;

        // Read all game-thread data before any await
        var senderName  = sender.TextValue;
        var messageText = message.TextValue;
        var slug        = ChatTypeHelper.GetSlug(normalizedType);

        var localPlayer = _clientState.LocalPlayer;
        var webhookUsername = localPlayer is not null
            ? $"{localPlayer.Name}@{localPlayer.HomeWorld.ValueNullable?.Name ?? "?"}"
            : senderName;

        var charName = localPlayer?.Name.ToString();
        var world    = localPlayer?.HomeWorld.ValueNullable?.Name.ToString();

        // Tell: track sender for autocomplete, optionally add Reply button
        MessageComponent? components = null;
        if (normalizedType == XivChatType.TellIncoming)
        {
            TrackTellPartner(config, senderName);

            var linkedUserId = config.CharLinks
                .FirstOrDefault(l => l.FfxivCharacter.Equals(senderName, StringComparison.OrdinalIgnoreCase))
                ?.DiscordUserId;

            if (linkedUserId.HasValue)
            {
                var customId = $"bridge:tell:reply:{Uri.EscapeDataString(senderName)}";
                components = new ComponentBuilder()
                    .WithButton("Reply", customId, ButtonStyle.Secondary, new Emoji("↩️"))
                    .Build();
            }
        }

        // Hand off to async continuation — all game data already captured above
        _ = FirePayloadsAsync(matchingMappings, webhookUsername, slug, messageText,
                              charName, world, components);
    }

    // ── Async dispatch ─────────────────────────────────────────────────────

    private async Task FirePayloadsAsync(List<ChannelMapping> mappings, string webhookUsername,
                                         string slug, string rawText,
                                         string? charName, string? world,
                                         MessageComponent? components)
    {
        var content = $"[{slug}] {_specialChars.Transform(rawText)}";

        string? avatarUrl = null;
        if (charName is not null && world is not null)
            avatarUrl = await _avatarService.GetAvatarUrlAsync(charName, world);

        if (OnDiscordMessage is null) return;

        foreach (var mapping in mappings)
        {
            var payload = new DiscordMessagePayload
            {
                ChannelId  = mapping.DiscordChannelId,
                WebhookUrl = mapping.WebhookUrl,
                Username   = webhookUsername,
                Content    = content,
                AvatarUrl  = avatarUrl,
                Components = components,
            };

            await OnDiscordMessage.Invoke(payload);
        }
    }

    // ── Tell partner tracking ──────────────────────────────────────────────

    private void TrackTellPartner(PluginConfig config, string senderName)
    {
        if (config.RecentTellPartners.Contains(senderName)) return;

        config.RecentTellPartners.Insert(0, senderName);

        if (config.RecentTellPartners.Count > 50)
            config.RecentTellPartners.RemoveAt(config.RecentTellPartners.Count - 1);

        _configStore.Save(config);
    }

    public void Dispose() => _chatGui.ChatMessage -= OnChatMessage;
}
