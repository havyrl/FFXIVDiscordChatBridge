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
                               IPlayerState playerState, BotService botService, AdminRequestService adminRequest,
                               IConfigStore configStore, IFramework framework)
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

        var locale  = Context.Interaction.UserLocale;
        var builder = new EmbedBuilder()
            .WithTitle(T("info.help.title"))
            .WithColor(0x478CFF);

        if (botService.Interactions is { } interactions)
        {
            var topLevel = interactions.Modules
                .Where(m => m.Parent == null)
                .OrderBy(m => m.SlashGroupName ?? m.SlashCommands.FirstOrDefault()?.Name ?? m.Name);

            foreach (var mod in topLevel)
            {
                if (mod.IsSlashGroup)
                {
                    var groupName = mod.SlashGroupName;
                    var key       = $"slash.{groupName}";
                    var desc      = Localizer.T(key, locale);
                    if (desc == key) desc = mod.Description ?? groupName;
                    builder.AddField($"/{groupName}", desc);
                }
                else
                {
                    foreach (var cmd in mod.SlashCommands.OrderBy(c => c.Name))
                    {
                        var key  = $"slash.{cmd.Name}";
                        var desc = Localizer.T(key, locale);
                        if (desc == key) desc = cmd.Description;
                        builder.AddField($"/{cmd.Name}", desc);
                    }
                }
            }
        }

        await RespondAsync(embed: builder.Build(), ephemeral: true);
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
            var ch = playerState.IsLoaded
                ? $"{playerState.CharacterName}@{playerState.HomeWorld.ValueNullable?.Name ?? "?"}"
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

    [SlashCommand("chattypes", "List all supported FFXIV chat type slugs with their names.")]
    public async Task ChatTypesAsync()
    {
        if (!guard.CanViewStatus(Context.User))
        {
            await RespondAsync(T("common.no_permission"), ephemeral: true);
            return;
        }

        var locale = Context.Interaction.UserLocale;

        // System/GM slugs are separated into their own field to keep the main list readable.
        var systemSlugs = new HashSet<string>
        {
            "none", "debug", "urgent", "notice", "e", "sysmsg", "syserror", "gathersysmsg",
            "errmsg", "alarm", "npctalk", "synthmsg", "npcannounce", "fcannounce", "fclogin",
            "sign", "random", "nnn",
            "gmtell", "gmsay", "gmshout", "gmyell", "gmp", "gmfc",
            "gmls1", "gmls2", "gmls3", "gmls4", "gmls5", "gmls6", "gmls7", "gmls8", "gmnn",
        };

        string FormatEntry(KeyValuePair<Dalamud.Game.Text.XivChatType, ChatTypeHelper.ChatTypeInfo> kvp)
            => $"`{kvp.Value.Slug}` — {ChatTypeHelper.GetLocalizedName(kvp.Key, Localizer, locale)}";

        var common = string.Join("\n", ChatTypeHelper.All
            .Where(kvp => !systemSlugs.Contains(kvp.Value.Slug))
            .Select(FormatEntry));

        var system = string.Join("\n", ChatTypeHelper.All
            .Where(kvp => systemSlugs.Contains(kvp.Value.Slug))
            .Select(FormatEntry));

        var embed = new EmbedBuilder()
            .WithTitle(T("info.chattypes.title"))
            .WithColor(0x478CFF)
            .AddField(T("info.chattypes.common"), common)
            .AddField(T("info.chattypes.system"), system)
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
            if (!playerState.IsLoaded) return ((string, string)?)null;
            return (playerState.CharacterName, playerState.HomeWorld.ValueNullable?.Name.ToString() ?? "?");
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
