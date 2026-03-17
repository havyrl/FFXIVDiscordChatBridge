using Dalamud.Plugin.Services;
using Discord;
using Discord.Interactions;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Discord;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Modules;

/// <summary>General bridge info commands: /help, /status, /who, /requestadmin</summary>
[DefaultMemberPermissions(GuildPermission.SendMessages)]
public sealed class InfoModule(ILocalizer localizer, PermissionGuard guard, IClientState clientState,
                               BotService botService, AdminRequestService adminRequest, IConfigStore configStore,
                               IFramework framework)
    : LocalizedModuleBase(localizer)
{
    [SlashCommand("requestadmin", "Request admin access for the FFXIV Discord Bridge.")]
    public async Task RequestAdminAsync()
    {
        if (configStore.Load().AdminDiscordUserId != 0)
        {
            await RespondAsync(T("info.requestadmin.already_configured"), ephemeral: true);
            return;
        }

        var name = Context.User.GlobalName ?? Context.User.Username;
        adminRequest.Submit(Context.User.Id, name);
        await RespondAsync(T("info.requestadmin.sent"), ephemeral: true);
    }

    [SlashCommand("help", "Lists all available bridge commands.")]
    public async Task HelpAsync()
    {
        if (!guard.CanViewStatus(Context.User))
        {
            await RespondAsync(T("common.no_permission"), ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(T("info.help.title"))
            .WithColor(0x478CFF)
            .AddField("/help",   T("info.help.help"))
            .AddField("/status", T("info.help.status"))
            .AddField("/who",    T("info.help.who"))
            .AddField("/say, /fc, /party, /yell, /shout", T("info.help.chat"))
            .AddField("/tell <character> <message>",      T("info.help.tell"))
            .AddField("/config channel",      T("info.help.config_channel"))
            .AddField("/config backchannel",  T("info.help.config_backchannel"))
            .AddField("/config webhook",      T("info.help.config_webhook"))
            .AddField("/config dm",           T("info.help.config_dm"))
            .AddField("/config permissions",  T("info.help.config_permissions"))
            .AddField("/config link",         T("info.help.config_link"))
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("status", "Shows bridge connection status and active channel mappings.")]
    public async Task StatusAsync()
    {
        if (!guard.CanViewStatus(Context.User))
        {
            await RespondAsync(T("common.no_permission"), ephemeral: true);
            return;
        }

        var connected = botService.IsConnected;
        var (loggedIn, character) = await framework.RunOnFrameworkThread(() =>
        {
            var li = clientState.IsLoggedIn;
            var ch = clientState.LocalPlayer is { } p
                ? $"{p.Name}@{p.HomeWorld.ValueNullable?.Name ?? "?"}"
                : T("info.status.no_character");
            return (li, ch);
        });

        var embed = new EmbedBuilder()
            .WithTitle(T("info.status.title"))
            .WithColor(new Color(connected ? 0x478CFFu : 0xD10303u))
            .AddField(T("info.status.bot_label"),       connected ? T("info.status.connected")    : T("info.status.disconnected"), inline: true)
            .AddField(T("info.status.ffxiv_label"),     loggedIn  ? T("info.status.logged_in")    : T("info.status.not_logged_in"), inline: true)
            .AddField(T("info.status.character_label"), character, inline: true)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("who", "Shows which FFXIV character is currently logged in.")]
    public async Task WhoAsync()
    {
        if (!guard.CanViewStatus(Context.User))
        {
            await RespondAsync(T("common.no_permission"), ephemeral: true);
            return;
        }

        var playerInfo = await framework.RunOnFrameworkThread(() =>
        {
            if (clientState.LocalPlayer is not { } p) return ((string, string)?)null;
            return (p.Name.ToString(), p.HomeWorld.ValueNullable?.Name.ToString() ?? "?");
        });

        if (playerInfo is null)
        {
            await RespondAsync(T("info.who.not_logged_in"), ephemeral: true);
            return;
        }

        var (name, world) = playerInfo.Value;
        await RespondAsync(T("info.who.playing_as", name, world), ephemeral: true);
    }
}
