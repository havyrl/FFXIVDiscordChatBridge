using Discord;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Interactions;

/// <summary>
/// Builds the full set of toggle buttons shown below the /config channel info embed.
/// Centralised here so both DutyToggleActionHandler and PartyInviteToggleActionHandler
/// always rebuild the complete button row instead of each managing only their own button.
/// </summary>
public static class ChannelInfoButtons
{
    public static MessageComponent Build(ILocalizer localizer, ulong channelId,
        bool isDuty, bool isPartyInvite, string? locale = null)
    {
        var dutyLabel  = isDuty
            ? localizer.T("config.channel.duty_btn_disable",  locale)
            : localizer.T("config.channel.duty_btn_enable",   locale);
        var partyLabel = isPartyInvite
            ? localizer.T("config.channel.party_btn_disable", locale)
            : localizer.T("config.channel.party_btn_enable",  locale);

        return new ComponentBuilder()
            .WithButton(dutyLabel,  $"bridge:config:duty-toggle:{channelId}",
                isDuty         ? ButtonStyle.Danger : ButtonStyle.Success)
            .WithButton(partyLabel, $"bridge:config:party-toggle:{channelId}",
                isPartyInvite  ? ButtonStyle.Danger : ButtonStyle.Success)
            .Build();
    }
}
