using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;
using Lumina.Excel.Sheets;

namespace FFXIVDiscordBridgePlugin.Chat;

/// <summary>
/// IGameEventSource that fires whenever a retainer sells an item.
/// Listens for <see cref="XivChatType.RetainerSale"/> chat messages, resolves the sold item's
/// icon from XIVAPI, and forwards to all ChannelMappings with <see cref="ChannelMapping.IsRetainerSale"/> = true.
/// </summary>
public sealed class RetainerSaleEventSource : IGameEventSource
{
    public event Func<DiscordMessagePayload, Task>? OnDiscordMessage;

    private readonly IChatGui _chatGui;
    private readonly IClientState _clientState;
    private readonly IConfigStore _configStore;
    private readonly IPluginLog _log;
    private readonly SpecialCharsHandler _specialChars;
    private readonly IDataManager _dataManager;

    public RetainerSaleEventSource(IChatGui chatGui, IClientState clientState,
                                   IConfigStore configStore, IPluginLog log,
                                   SpecialCharsHandler specialChars, IDataManager dataManager)
    {
        _chatGui = chatGui;
        _clientState = clientState;
        _configStore = configStore;
        _log = log;
        _specialChars = specialChars;
        _dataManager = dataManager;
    }

    public void Initialize() => _chatGui.ChatMessage += OnChatMessage;

    // ── IChatGui handler ───────────────────────────────────────────────────

    private void OnChatMessage(XivChatType type, int timestamp,
                               ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (type != XivChatType.RetainerSale) return;
        if (OnDiscordMessage is null) return;

        var config  = _configStore.Load();
        var targets = config.ChannelMappings.Where(m => m.IsRetainerSale).ToList();
        if (targets.Count == 0) return;

        var messageText = _specialChars.Transform(message.TextValue);
        var playerName  = _clientState.LocalPlayer?.Name.ToString() ?? "Retainer";

        // Try to resolve item icon via ItemPayload → IDataManager lookup
        string? avatarUrl = null;
        var itemPayload = message.Payloads.OfType<ItemPayload>().FirstOrDefault();
        if (itemPayload is not null)
        {
            var item = _dataManager.GetExcelSheet<Item>()?.GetRow(itemPayload.ItemId);
            if (item.HasValue)
            {
                var iconId     = (uint)item.Value.Icon;
                var iconFolder = $"{iconId / 1000 * 1000:D6}";
                var iconFile   = $"{iconId:D6}";
                avatarUrl = $"https://beta.xivapi.com/api/1/asset?path=ui%2Ficon%2F{iconFolder}%2F{iconFile}_hr1.tex&format=png";

                _log.Debug("[RetainerSaleEventSource] Sale: {Item} (icon {Id}) — {Count} channel(s)",
                           item.Value.Name.ExtractText(), iconId, targets.Count);
            }
        }
        else
        {
            _log.Debug("[RetainerSaleEventSource] RetainerSale without ItemPayload — {Count} channel(s)", targets.Count);
        }

        foreach (var mapping in targets)
        {
            var payload = new DiscordMessagePayload
            {
                ChannelId  = mapping.DiscordChannelId,
                WebhookUrl = mapping.WebhookUrl,
                Username   = playerName,
                Content    = messageText,
                AvatarUrl  = avatarUrl,
            };
            _ = OnDiscordMessage.Invoke(payload);
        }
    }

    public void Dispose() => _chatGui.ChatMessage -= OnChatMessage;
}
