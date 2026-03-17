using Dalamud.Plugin.Services;
using Discord;
using Discord.WebSocket;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Interactions;

/// <summary>
/// IDiscordActionHandler for the "Join" button on Duty Finder pop notifications.
/// Custom ID: bridge:duty:join
///
/// When clicked, checks whether the ContentsFinderConfirm addon is currently visible
/// (i.e. the duty pop is still active) and fires the Commence callback to accept it.
/// Must be whitelisted or be the bridge admin to use.
/// </summary>
public sealed class DutyJoinActionHandler(ILocalizer localizer, PermissionGuard guard,
                                          IGameGui gameGui, IFramework framework)
    : IDiscordActionHandler
{
    private const string CustomId = "bridge:duty:join";

    public bool CanHandle(string customId) => customId == CustomId;

    public async Task HandleAsync(SocketInteraction interaction)
    {
        var locale = interaction.UserLocale;

        if (!guard.CanUseChatCommands(interaction.User))
        {
            await interaction.RespondAsync(localizer.T("duty.join.no_permission", locale), ephemeral: true);
            return;
        }

        var accepted = false;
        await framework.RunOnFrameworkThread(() => { accepted = TryAcceptDuty(); });

        if (accepted && interaction is SocketMessageComponent button)
        {
            // Remove the Join button from the original message
            await button.UpdateAsync(msg => msg.Components = new ComponentBuilder().Build());
        }
        else
        {
            var reply = localizer.T("duty.join.no_popup", locale);
            await interaction.RespondAsync(reply, ephemeral: true);
        }
    }

    private unsafe bool TryAcceptDuty()
    {
        var addonWrapper = gameGui.GetAddonByName("ContentsFinderConfirm");
        var addonPtr     = (nint)addonWrapper;
        if (addonPtr == IntPtr.Zero) return false;

        var addon = (AtkUnitBase*)addonPtr;
        if (!addon->IsVisible) return false;

        // Fire callback index 8 = Commence/Accept button
        var values = stackalloc AtkValue[1];
        values[0].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int;
        values[0].Int  = 8;
        addon->FireCallback(1, values);
        return true;
    }
}
