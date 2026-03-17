using Dalamud.Plugin.Services;
using Discord;
using Discord.Interactions;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Discord;

namespace FFXIVDiscordBridgePlugin.Discord.Modules;

/// <summary>General bridge info commands: /help, /status, /who, /requestadmin</summary>
[DefaultMemberPermissions(GuildPermission.SendMessages)]
public sealed class InfoModule(PermissionGuard guard, IClientState clientState, BotService botService,
                               AdminRequestService adminRequest, IConfigStore configStore)
    : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("requestadmin", "Request admin access for the FFXIV Discord Bridge.")]
    public async Task RequestAdminAsync()
    {
        if (configStore.Load().AdminDiscordUserId != 0)
        {
            await RespondAsync("Bridge admin is already configured. Remove them in the plugin settings first.", ephemeral: true);
            return;
        }

        var name = Context.User.GlobalName ?? Context.User.Username;
        adminRequest.Submit(Context.User.Id, name);
        await RespondAsync("Request sent! Please approve it in FFXIV.", ephemeral: true);
    }

    [SlashCommand("help", "Lists all available bridge commands.")]
    public async Task HelpAsync()
    {
        if (!guard.CanViewStatus(Context.User))
        {
            await RespondAsync("You don't have permission to use this command.", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("FFXIV Bridge — Commands")
            .WithColor(0x478CFF)
            .AddField("/help",   "Show this help.")
            .AddField("/status", "Show bridge connection status and active channel mappings.")
            .AddField("/who",    "Show which FFXIV character is currently logged in.")
            .AddField("/say, /fc, /party, /yell, /shout",
                      "Send a message to the respective FFXIV chat channel.")
            .AddField("/tell <character> <message>",
                      "Send a tell to an FFXIV player. Character name supports autocomplete.")
            .AddField("/config channel",
                      "Add, remove or list FFXIV→Discord channel mappings.")
            .AddField("/config backchannel",
                      "Set or clear the Discord→FFXIV back-channel for a mapping.")
            .AddField("/config webhook",
                      "Set the webhook URL for a channel mapping.")
            .AddField("/config dm",
                      "Enable or disable DM as an inbound channel.")
            .AddField("/config permissions",
                      "Add, remove or list whitelist entries.")
            .AddField("/config link",
                      "Link or unlink an FFXIV character to a Discord user.")
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("status", "Shows bridge connection status and active channel mappings.")]
    public async Task StatusAsync()
    {
        if (!guard.CanViewStatus(Context.User))
        {
            await RespondAsync("You don't have permission to use this command.", ephemeral: true);
            return;
        }

        var connected = botService.IsConnected;
        var loggedIn  = clientState.IsLoggedIn;
        var character = clientState.LocalPlayer is { } p
            ? $"{p.Name}@{p.HomeWorld.ValueNullable?.Name ?? "?"}"
            : "None";

        var embed = new EmbedBuilder()
            .WithTitle("FFXIV Bridge — Status")
            .WithColor(new Color(connected ? 0x478CFFu : 0xD10303u))
            .AddField("Bot",       connected ? "✅ Connected" : "❌ Disconnected", inline: true)
            .AddField("FFXIV",     loggedIn  ? "✅ Logged in" : "⚠️ Not logged in", inline: true)
            .AddField("Character", character, inline: true)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("who", "Shows which FFXIV character is currently logged in.")]
    public async Task WhoAsync()
    {
        if (!guard.CanViewStatus(Context.User))
        {
            await RespondAsync("You don't have permission to use this command.", ephemeral: true);
            return;
        }

        if (clientState.LocalPlayer is not { } player)
        {
            await RespondAsync("No character is currently logged in.", ephemeral: true);
            return;
        }

        var name  = player.Name.ToString();
        var world = player.HomeWorld.ValueNullable?.Name.ToString() ?? "?";
        await RespondAsync($"Currently playing as **{name}@{world}**.", ephemeral: true);
    }
}
