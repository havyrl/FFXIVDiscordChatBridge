using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
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
    private readonly IPlayerState _playerState;
    private readonly IConfigStore _configStore;
    private readonly IPluginLog _log;
    private readonly MessageConverter _messageConverter;
    private readonly MapImageService _mapImageService;
    private readonly CharacterAvatarService _avatarService;
    private readonly ChatConfirmationService _confirmations;
    private readonly LinkshellNameService _linkshellNames;

    public ChatEventSource(IChatGui chatGui, IPlayerState playerState,
                           IConfigStore configStore, IPluginLog log,
                           MessageConverter messageConverter, MapImageService mapImageService,
                           CharacterAvatarService avatarService,
                           ChatConfirmationService confirmations, LinkshellNameService linkshellNames)
    {
        _chatGui = chatGui;
        _playerState = playerState;
        _configStore = configStore;
        _log = log;
        _messageConverter = messageConverter;
        _mapImageService = mapImageService;
        _avatarService = avatarService;
        _confirmations = confirmations;
        _linkshellNames = linkshellNames;
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

        // Notify any pending slash-command confirmation that this message arrived
        _confirmations.TryConfirm(normalizedType, message.TextValue);

        // Read all game-thread data before any await.
        // ToDiscord() is synchronous and must run here on the game thread
        // while the SeString payloads are still valid.
        var senderName  = sender.TextValue;
        var messageText = _messageConverter.ToDiscord(message);
        var slug        = _linkshellNames.TryGetSlug(normalizedType) ?? ChatTypeHelper.GetSlug(normalizedType);
        var mapLink     = message.Payloads.OfType<MapLinkPayload>().FirstOrDefault();
        var isSystem    = ChatTypeHelper.IsSystemType(normalizedType);

        var playerCharName = _playerState.IsLoaded ? _playerState.CharacterName : null;
        var localWorld     = _playerState.IsLoaded ? _playerState.HomeWorld.ValueNullable?.Name.ToString() : null;

        // For cross-world players sender.TextValue concatenates name+world without separator
        // (e.g. "R'yloh TiaOdin"). PlayerPayload carries them separately.
        var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        var payloadName   = playerPayload?.PlayerName;
        var payloadWorld  = playerPayload?.World.ValueNullable?.Name.ToString();

        // TellOutgoing: sender SeString contains the *recipient*, not the local player
        var isTellOut = normalizedType == XivChatType.TellOutgoing;
        var isOwnMessage = !isSystem && _playerState.IsLoaded && (
            isTellOut ||
            string.Equals(payloadName ?? senderName, playerCharName, StringComparison.OrdinalIgnoreCase));

        if (!isSystem && !isOwnMessage && playerPayload is null)
            _log.Warning("[ChatEventSource] No PlayerPayload in sender SeString — falling back to TextValue for '{Name}'", senderName);

        var webhookUsername = isSystem
            ? $"FFXIV System - {playerCharName}@{localWorld ?? "?"}".TrimEnd()
            : isOwnMessage
                ? $"{playerCharName}@{localWorld ?? "?"}"
                : payloadName is not null
                    ? $"{payloadName}@{payloadWorld ?? localWorld ?? "?"}"
                    : $"{senderName}@{localWorld ?? "?"}";

        // Strip rank icons / FFXIV private-use chars so Lodestone can find the character
        var charName = isSystem ? null : (payloadName ?? ExtractCharacterName(senderName));
        var world    = !isSystem ? (payloadWorld ?? localWorld) : null;

        if (!isSystem && localWorld is null)
            _log.Warning("[ChatEventSource] HomeWorld resolved to null — avatar will be skipped for {Name}", charName ?? "(null)");

        // Tell: track sender for autocomplete, optionally add Reply button
        MessageComponent? components = null;
        if (normalizedType == XivChatType.TellIncoming)
        {
            // Build "Name@World" for the autocomplete cache.
            // Cross-world: PlayerPayload has both name and world separated cleanly.
            // Same-server: PlayerPayload may be absent; fall back to senderName + localWorld.
            var tellSender = payloadName is not null
                ? $"{payloadName}@{payloadWorld ?? localWorld ?? "?"}"
                : localWorld is not null
                    ? $"{senderName}@{localWorld}"
                    : senderName;
            TrackTellPartner(config, tellSender);

            var linkedUserId = config.CharLinks
                .FirstOrDefault(l => l.FfxivCharacter.Equals(tellSender, StringComparison.OrdinalIgnoreCase))
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
                              charName, world, components, mapLink);
    }

    // ── Async dispatch ─────────────────────────────────────────────────────

    private async Task FirePayloadsAsync(List<ChannelMapping> mappings, string webhookUsername,
                                         string slug, string rawText,
                                         string? charName, string? world,
                                         MessageComponent? components,
                                         MapLinkPayload? mapLink = null)
    {
        _log.Debug("[ChatEventSource] FirePayloads start: mappings={Count} user={User} slug={Slug} text={Text}",
                   mappings.Count, webhookUsername, slug, rawText);
        try
        {
            var content = $"[{slug}] {rawText}";

            string? avatarUrl;
            if (charName is not null && world is not null)
                avatarUrl = await _avatarService.GetAvatarUrlAsync(charName, world);
            else
                avatarUrl = CharacterAvatarService.FallbackAvatarUrl;

            // GeneratePinImage is CPU-heavy (texture decode + image processing).
            // Run off the game thread, after the first await above has already yielded.
            var mapImage = mapLink is not null
                ? await Task.Run(() => _mapImageService.GeneratePinImage(mapLink))
                : null;

            _log.Debug("[ChatEventSource] Avatar resolved: {AvatarUrl}", avatarUrl ?? "(null→fallback)");

            if (OnDiscordMessage is null)
            {
                _log.Warning("[ChatEventSource] OnDiscordMessage is null — payload dropped for {User}", webhookUsername);
                return;
            }

            foreach (var mapping in mappings)
            {
                var payload = new DiscordMessagePayload
                {
                    ChannelId          = mapping.DiscordChannelId,
                    WebhookUrl         = mapping.WebhookUrl,
                    IsDm               = mapping.IsDm,
                    Username           = webhookUsername,
                    Content            = content,
                    AvatarUrl          = avatarUrl,
                    Components         = components,
                    Attachment         = mapImage,
                    AttachmentFilename = mapImage is not null ? "map.jpg" : null,
                };

                await OnDiscordMessage.Invoke(payload);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[ChatEventSource] FirePayloadsAsync failed for user={User} text={Text}", webhookUsername, rawText);
        }
    }

    // ── Tell partner tracking ──────────────────────────────────────────────

    private void TrackTellPartner(PluginConfig config, string senderName)
    {
        if (config.RecentTellPartners.Contains(senderName)) return;

        config.RecentTellPartners.Insert(0, senderName);

        if (config.RecentTellPartners.Count > 50)
            config.RecentTellPartners.RemoveAt(config.RecentTellPartners.Count - 1);

        _ = Task.Run(() => _configStore.Save(config));
    }

    public void Dispose() => _chatGui.ChatMessage -= OnChatMessage;

    /// <summary>
    /// Strips FFXIV rank icons and private-use characters from a sender name so it
    /// can be used for Lodestone character lookups. FFXIV names only contain letters,
    /// spaces, apostrophes, and hyphens.
    /// </summary>
    private static string? ExtractCharacterName(string senderName)
    {
        var clean = new string([..senderName.Where(c => char.IsLetter(c) || c == ' ' || c == '\'' || c == '-')]).Trim();
        return clean.Length > 0 ? clean : null;
    }
}
