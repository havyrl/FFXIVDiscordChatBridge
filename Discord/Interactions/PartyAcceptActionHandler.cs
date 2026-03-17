using Dalamud.Plugin.Services;
using Discord;
using Discord.WebSocket;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Interactions;

/// <summary>
/// IDiscordActionHandler for the "Accept" button on party invite notifications.
/// Custom ID: bridge:party:accept
///
/// Uses AgentPartyInvite to check whether a party invite is pending, then fires
/// ReceiveEvent(0) on the agent to accept it. Must be whitelisted or be the bridge admin.
/// </summary>
public sealed class PartyAcceptActionHandler(ILocalizer localizer, PermissionGuard guard,
                                             IGameGui gameGui, IFramework framework, IPluginLog log)
    : IDiscordActionHandler
{
    private const string CustomId = "bridge:party:accept";

    public bool CanHandle(string customId) => customId == CustomId;

    public async Task HandleAsync(SocketInteraction interaction)
    {
        var locale = interaction.UserLocale;

        if (!guard.CanUseChatCommands(interaction.User))
        {
            await interaction.RespondAsync(localizer.T("party.accept.no_permission", locale), ephemeral: true);
            return;
        }

        var accepted = false;
        await framework.RunOnFrameworkThread(() => { accepted = TryInteract(); });

        if (accepted && interaction is SocketMessageComponent button)
            await button.UpdateAsync(msg => msg.Components = new ComponentBuilder().Build());
        else if (!accepted)
            await interaction.RespondAsync(localizer.T("party.accept.no_popup", locale), ephemeral: true);
    }

    private unsafe bool TryInteract()
    {
        var agent = AgentPartyInvite.Instance();
        log.Debug("[PartyAcceptActionHandler] agent={Agent} active={Active} addonId={AddonId} addonShown={Shown}",
            (nint)agent,
            agent != null && agent->IsAgentActive(),
            agent != null ? agent->AddonId : 0,
            agent != null && agent->IsAddonShown());

        if (agent == null || !agent->IsAgentActive()) return false;

        // Also log what GetAddonByName returns for common names
        foreach (var name in new[] { "_PartyInvite", "PartyInvite", "_Notification", "SelectYesno" })
        {
            var ptr = (nint)gameGui.GetAddonByName(name);
            if (ptr != IntPtr.Zero)
                log.Debug("[PartyAcceptActionHandler] GetAddonByName({Name})={Ptr:X}", name, (long)ptr);
        }

        // Fire ReceiveEvent(0) = Accept on the agent directly
        var retVal = stackalloc AtkValue[1];
        retVal[0] = default;
        var values = stackalloc AtkValue[1];
        values[0].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int;
        values[0].Int  = 0;
        agent->ReceiveEvent(retVal, values, 1, 0);
        return true;
    }
}
