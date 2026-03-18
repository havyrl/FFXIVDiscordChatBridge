using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Discord;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Gui;

/// <summary>
/// Main plugin window opened via /discordbridge or the Dalamud plugin menu.
/// Tabs: Bot Settings | Channels | Whitelist | Character Links
/// </summary>
public sealed class MainWindow(IConfigStore configStore, BotService botService, ILocalizer localizer)
    : Window("FFXIV Discord Bridge", ImGuiWindowFlags.None)
{
    private PluginConfig _config = null!;

    // ── Per-tab edit state ─────────────────────────────────────────────────
    private string _botToken        = string.Empty;
    private string _adminUserId     = string.Empty;
    private bool   _tokenDirty;
    private int    _primaryGuildIdx = 0;   // 0 = global, 1+ = guild index in AvailableGuilds

    // Channel tab
    private string _newChannelId    = string.Empty;
    private string _newWebhookUrl   = string.Empty;
    private string _newLabel        = string.Empty;
    private int    _selectedTypeIdx = 0;

    // Whitelist tab
    private string _wlDiscordId     = string.Empty;
    private bool   _wlIsRole;
    private bool   _wlBackChannel, _wlTell, _wlChat, _wlStatus = true;

    // Char link tab
    private string _linkDiscordId   = string.Empty;
    private string _linkCharacter   = string.Empty;

    // Formatting tab
    private static readonly string[] LocaleOptions = ["de", "en", "fr", "ja"];
    private int    _fmtDbIdx;
    private int    _fmtLocaleIdx;
    private string _fmtCustomUrl = string.Empty;

    public override void OnOpen()
    {
        _config      = configStore.Load();
        _botToken    = _config.BotToken;
        _adminUserId = _config.AdminDiscordUserId == 0 ? string.Empty
                       : _config.AdminDiscordUserId.ToString();
        _tokenDirty  = false;

        _primaryGuildIdx = 0;
        if (_config.PrimaryGuildId != 0)
        {
            var guilds = botService.AvailableGuilds;
            for (var i = 0; i < guilds.Count; i++)
            {
                if (guilds[i].Id == _config.PrimaryGuildId) { _primaryGuildIdx = i + 1; break; }
            }
        }

        _fmtDbIdx = ItemDatabaseDefinition.Builtin
            .Select((d, i) => (d, i))
            .FirstOrDefault(x => x.d.Id == _config.ItemDatabaseId).i;
        // If not found (e.g. custom), select last entry which will show Custom controls
        if (_fmtDbIdx == 0 && _config.ItemDatabaseId != ItemDatabaseDefinition.Builtin[0].Id)
            _fmtDbIdx = ItemDatabaseDefinition.Builtin.Count; // Custom slot
        _fmtLocaleIdx = Array.IndexOf(LocaleOptions, _config.ItemLinkLocale);
        if (_fmtLocaleIdx < 0) _fmtLocaleIdx = 0;
        _fmtCustomUrl = _config.CustomItemUrlTemplate;
    }

    public override void Draw()
    {
        _config ??= configStore.Load();

        if (!ImGui.BeginTabBar("##tabs")) return;

        if (ImGui.BeginTabItem("Bot Settings"))  { DrawBotTab();        ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Channels"))       { DrawChannelsTab();   ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Whitelist"))      { DrawWhitelistTab();  ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Character Links")){ DrawCharLinksTab();  ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Formatting"))     { DrawFormattingTab(); ImGui.EndTabItem(); }

        ImGui.EndTabBar();
    }

    // ── Bot Settings ───────────────────────────────────────────────────────

    private void DrawBotTab()
    {
        var status = botService.IsConnected ? "Connected ✅" : "Disconnected ❌";
        ImGui.TextColored(botService.IsConnected ? new Vector4(0.28f, 0.55f, 1f, 1f)
                                                 : new Vector4(0.82f, 0.05f, 0.01f, 1f),
                          $"Status: {status}");
        ImGui.Spacing();

        ImGui.Text("Bot Token");
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputText("##token", ref _botToken, 100, ImGuiInputTextFlags.Password))
            _tokenDirty = true;

        ImGui.Spacing();
        ImGui.Text("Admin Discord User ID");
        ImGui.SetNextItemWidth(200);
        ImGui.InputText("##adminid", ref _adminUserId, 24);

        ImGui.Spacing();
        ImGui.Text("Default Discord Server");
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var guilds  = botService.AvailableGuilds;
        var options = new string[guilds.Count + 1];
        options[0]  = "Global (all servers)";
        for (var i = 0; i < guilds.Count; i++)
            options[i + 1] = guilds[i].Name;
        if (!botService.IsConnected)
            ImGui.TextDisabled("(connect the bot first to see available servers)");
        else
            ImGui.Combo("##primaryguild", ref _primaryGuildIdx, options, options.Length);

        ImGui.Spacing();
        if (ImGui.Button("Save & Restart Bot"))
        {
            _config.BotToken = _botToken;
            if (ulong.TryParse(_adminUserId, out var uid))
                _config.AdminDiscordUserId = uid;
            _config.PrimaryGuildId = _primaryGuildIdx == 0 ? 0 : guilds[_primaryGuildIdx - 1].Id;
            configStore.Save(_config);
            _tokenDirty = false;

            _ = Task.Run(async () =>
            {
                await botService.StopAsync();
                await botService.StartAsync();
            });
        }

        if (_tokenDirty)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0.8f, 0f, 1f), "Unsaved changes");
        }
    }

    // ── Channels ───────────────────────────────────────────────────────────

    private void DrawChannelsTab()
    {
        ImGui.Text("Channel Mappings");
        ImGui.Separator();

        if (_config.ChannelMappings.Count == 0)
            ImGui.TextDisabled("No mappings configured yet.");

        ChannelMapping? toRemove = null;
        var shiftHeld = ImGui.GetIO().KeyShift;
        foreach (var mapping in _config.ChannelMappings)
        {
            var label  = mapping.IsDm ? "DM" : (string.IsNullOrEmpty(mapping.Label)
                         ? mapping.DiscordChannelId.ToString() : mapping.Label);
            var types  = string.Join(", ", mapping.InboundChatTypes.Select(ChatTypeHelper.GetSlug));
            var back   = mapping.BackChannelType.HasValue
                         ? $" → back: {ChatTypeHelper.GetSlug(mapping.BackChannelType.Value)}" : "";

            ImGui.BulletText($"{label}  [{types}]{back}");
            ImGui.SameLine();
            if (mapping.BackChannelType.HasValue && !mapping.IsDm)
            {
                var delMsg = mapping.DeleteBackChannelMessages;
                ImGui.PushID($"delbck{mapping.DiscordChannelId}");
                if (ImGui.Checkbox("Auto-delete", ref delMsg))
                {
                    mapping.DeleteBackChannelMessages = delMsg;
                    configStore.Save(_config);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Originalnachricht in Discord l\u00f6schen, nachdem sie ins Spiel weitergeleitet wurde.\nErfordert 'Nachrichten verwalten'-Berechtigung f\u00fcr den Bot.");
                ImGui.PopID();
                ImGui.SameLine();
            }
            if (!shiftHeld) ImGui.BeginDisabled();
            ImGui.PushID($"rm{mapping.DiscordChannelId}");
            if (ImGuiComponents.IconButton(FontAwesomeIcon.TrashAlt))
                toRemove = mapping;
            ImGui.PopID();
            if (!shiftHeld) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !shiftHeld)
                ImGui.SetTooltip(localizer.T("gui.main.delete_hold_shift"));
        }

        if (toRemove is not null)
        {
            _config.ChannelMappings.Remove(toRemove);
            configStore.Save(_config);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Add Mapping");

        ImGui.SetNextItemWidth(160);
        ImGui.InputText("Channel ID##chid", ref _newChannelId, 24);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(240);
        ImGui.InputText("Webhook URL##wh", ref _newWebhookUrl, 256);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("Label##lbl", ref _newLabel, 48);

        var typeNames = ChatTypeHelper.All.Values.Select(v => v.FancyName).ToArray();
        ImGui.SetNextItemWidth(200);
        ImGui.Combo("Chat Type##ct", ref _selectedTypeIdx, typeNames, typeNames.Length);

        if (ImGui.Button("Add##addch"))
        {
            if (ulong.TryParse(_newChannelId, out var chId) && !string.IsNullOrWhiteSpace(_newWebhookUrl))
            {
                var chatType = ChatTypeHelper.All.Keys.ElementAt(_selectedTypeIdx);
                var existing = _config.ChannelMappings.FirstOrDefault(m => m.DiscordChannelId == chId);
                if (existing is null)
                {
                    existing = new ChannelMapping
                    {
                        DiscordChannelId = chId,
                        WebhookUrl       = _newWebhookUrl,
                        Label            = _newLabel,
                    };
                    _config.ChannelMappings.Add(existing);
                }
                if (!existing.InboundChatTypes.Contains(chatType))
                    existing.InboundChatTypes.Add(chatType);

                configStore.Save(_config);
                _newChannelId = _newWebhookUrl = _newLabel = string.Empty;
            }
        }
    }

    // ── Whitelist ──────────────────────────────────────────────────────────

    private void DrawWhitelistTab()
    {
        ImGui.Text("Whitelist");
        ImGui.Separator();

        WhitelistEntry? toRemove = null;
        var shiftHeld = ImGui.GetIO().KeyShift;
        foreach (var entry in _config.Whitelist)
        {
            var kind  = entry.IsRole ? "Role" : "User";
            var perms = BuildPermString(entry.Permissions);
            ImGui.BulletText($"[{kind}] {entry.DiscordId}  ({perms})");
            ImGui.SameLine();
            if (!shiftHeld) ImGui.BeginDisabled();
            ImGui.PushID($"wlrm{entry.DiscordId}");
            if (ImGuiComponents.IconButton(FontAwesomeIcon.TrashAlt))
                toRemove = entry;
            ImGui.PopID();
            if (!shiftHeld) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !shiftHeld)
                ImGui.SetTooltip(localizer.T("gui.main.delete_hold_shift"));
        }
        if (toRemove is not null)
        {
            _config.Whitelist.Remove(toRemove);
            configStore.Save(_config);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Add Entry");

        ImGui.SetNextItemWidth(180);
        ImGui.InputText("User/Role ID##wlid", ref _wlDiscordId, 24);
        ImGui.SameLine();
        ImGui.Checkbox("Is Role##wlrole", ref _wlIsRole);

        ImGui.Checkbox("Back-channel##wlbc", ref _wlBackChannel); ImGui.SameLine();
        ImGui.Checkbox("Tell##wltell",        ref _wlTell);        ImGui.SameLine();
        ImGui.Checkbox("Chat##wlchat",        ref _wlChat);        ImGui.SameLine();
        ImGui.Checkbox("Status##wlst",        ref _wlStatus);

        if (ImGui.Button("Add##wladd") && ulong.TryParse(_wlDiscordId, out var wlId))
        {
            _config.Whitelist.RemoveAll(e => e.DiscordId == wlId);
            _config.Whitelist.Add(new WhitelistEntry
            {
                DiscordId   = wlId,
                IsRole      = _wlIsRole,
                Permissions = new WhitelistPermissions
                {
                    CanSendToBackChannel = _wlBackChannel,
                    CanSendTell          = _wlTell,
                    CanUseChatCommands   = _wlChat,
                    CanViewStatus        = _wlStatus,
                },
            });
            configStore.Save(_config);
            _wlDiscordId = string.Empty;
        }
    }

    // ── Character Links ────────────────────────────────────────────────────

    private void DrawCharLinksTab()
    {
        ImGui.Text("FFXIV Character ↔ Discord User Links");
        ImGui.TextDisabled("Linked characters get a Reply button on incoming Tell messages.");
        ImGui.Separator();

        CharLink? toRemove = null;
        var shiftHeld = ImGui.GetIO().KeyShift;
        foreach (var link in _config.CharLinks)
        {
            ImGui.BulletText($"{link.FfxivCharacter}  ↔  {link.DiscordUserId}");
            ImGui.SameLine();
            if (!shiftHeld) ImGui.BeginDisabled();
            ImGui.PushID($"clrm{link.DiscordUserId}");
            if (ImGuiComponents.IconButton(FontAwesomeIcon.TrashAlt))
                toRemove = link;
            ImGui.PopID();
            if (!shiftHeld) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !shiftHeld)
                ImGui.SetTooltip(localizer.T("gui.main.delete_hold_shift"));
        }
        if (toRemove is not null)
        {
            _config.CharLinks.Remove(toRemove);
            configStore.Save(_config);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Add Link");

        ImGui.SetNextItemWidth(200);
        ImGui.InputText("Character (Name@World)##clchar", ref _linkCharacter, 64);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180);
        ImGui.InputText("Discord User ID##cluid", ref _linkDiscordId, 24);

        if (ImGui.Button("Link##cladd")
            && ulong.TryParse(_linkDiscordId, out var clId)
            && !string.IsNullOrWhiteSpace(_linkCharacter))
        {
            _config.CharLinks.RemoveAll(l => l.DiscordUserId == clId);
            _config.CharLinks.Add(new CharLink { DiscordUserId = clId, FfxivCharacter = _linkCharacter });
            configStore.Save(_config);
            _linkDiscordId = _linkCharacter = string.Empty;
        }
    }

    // ── Formatting ─────────────────────────────────────────────────────────

    private void DrawFormattingTab()
    {
        ImGui.Text("Item Link Database");
        ImGui.TextDisabled("Where item links from FFXIV chat are sent (FFXIV \u2192 Discord).");
        ImGui.Spacing();

        var builtin    = ItemDatabaseDefinition.Builtin;
        var dbNames    = builtin.Select(d => d.DisplayName).Append("Custom").ToArray();
        var isCustom   = _fmtDbIdx >= builtin.Count;

        ImGui.SetNextItemWidth(160);
        ImGui.Combo("Database##fmtdb", ref _fmtDbIdx, dbNames, dbNames.Length);

        if (isCustom)
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputText("URL Template##fmturl", ref _fmtCustomUrl, 256);
            ImGui.TextDisabled("Placeholders: {id} = item ID, {locale} = locale string.");
        }

        ImGui.Spacing();
        ImGui.Text("Link Locale");
        ImGui.TextDisabled("Language used in database URLs (de, en, fr, ja).");
        ImGui.SetNextItemWidth(80);
        ImGui.Combo("Locale##fmtloc", ref _fmtLocaleIdx, LocaleOptions, LocaleOptions.Length);

        ImGui.Spacing();
        if (ImGui.Button("Save##fmtsave"))
        {
            _config.ItemDatabaseId        = isCustom ? "custom" : builtin[_fmtDbIdx].Id;
            _config.ItemLinkLocale        = LocaleOptions[_fmtLocaleIdx];
            _config.CustomItemUrlTemplate = _fmtCustomUrl;
            configStore.Save(_config);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string BuildPermString(WhitelistPermissions p)
    {
        var parts = new List<string>();
        if (p.CanSendToBackChannel) parts.Add("back-channel");
        if (p.CanSendTell)          parts.Add("tell");
        if (p.CanUseChatCommands)   parts.Add("chat");
        if (p.CanViewStatus)        parts.Add("status");
        return parts.Count > 0 ? string.Join(", ", parts) : "none";
    }
}
