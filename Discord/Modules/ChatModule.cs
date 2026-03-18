using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Discord;
using Discord.Interactions;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Modules;

/// <summary>
/// Direct FFXIV chat commands: /say, /fc, /party, /yell, /shout, /ls, /cwl, /tell
/// All commands require the user to be on the whitelist (CanUseChatCommands),
/// except /tell which requires CanSendTell.
/// Calling any command without a message (admin only) sets the back-channel type
/// for the current Discord channel, so that plain messages typed there are forwarded to FFXIV.
/// The ephemeral "✅ Gesendet." is auto-dismissed only when the matching message
/// bounces back from FFXIV through ChatEventSource (confirmed delivery).
/// If no confirmation arrives within 15 s, the ephemeral stays as a hint.
/// </summary>
public sealed class ChatModule(ILocalizer localizer, PermissionGuard guard, GameChatSender chatSender,
                               ChatConfirmationService confirmations, IFramework framework,
                               IConfigStore configStore, MessageConverter messageConverter, IChatGui chatGui)
    : LocalizedModuleBase(localizer)
{
    [SlashCommand("say", "Send a /say message in FFXIV, or set this channel as Say back-channel.")]
    public async Task SayAsync([Summary("message", "Message text")] string? message = null)
        => await DispatchAsync(XivChatType.Say, message);

    [SlashCommand("yell", "Send a /yell message in FFXIV, or set this channel as Yell back-channel.")]
    public async Task YellAsync([Summary("message", "Message text")] string? message = null)
        => await DispatchAsync(XivChatType.Yell, message);

    [SlashCommand("shout", "Send a /shout message in FFXIV, or set this channel as Shout back-channel.")]
    public async Task ShoutAsync([Summary("message", "Message text")] string? message = null)
        => await DispatchAsync(XivChatType.Shout, message);

    [SlashCommand("fc", "Send a /fc message in FFXIV, or set this channel as FC back-channel.")]
    public async Task FcAsync([Summary("message", "Message text")] string? message = null)
        => await DispatchAsync(XivChatType.FreeCompany, message);

    [SlashCommand("party", "Send a /party message in FFXIV, or set this channel as Party back-channel.")]
    public async Task PartyAsync([Summary("message", "Message text")] string? message = null)
        => await DispatchAsync(XivChatType.Party, message);

    [SlashCommand("nn", "Send a /novice message in FFXIV (Novice Network), or set this channel as NN back-channel.")]
    public async Task NnAsync([Summary("message", "Message text")] string? message = null)
        => await DispatchAsync(XivChatType.NoviceNetwork, message);

    [SlashCommand("echo", "Send an /echo message in FFXIV (visible only to yourself).")]
    public async Task EchoAsync([Summary("message", "Message text")] string message)
    {
        var converted = messageConverter.ToGameText(message);
        var localMsg  = messageConverter.BuildLocalMessage(message);
        await SendChatAsync($"/e {converted}", XivChatType.Echo, converted, requireChat: true, localMsg);
    }

    private static readonly XivChatType[] LsTypes =
    [
        XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3, XivChatType.Ls4,
        XivChatType.Ls5, XivChatType.Ls6, XivChatType.Ls7, XivChatType.Ls8,
    ];

    private static readonly XivChatType[] CwlTypes =
    [
        XivChatType.CrossLinkShell1, XivChatType.CrossLinkShell2, XivChatType.CrossLinkShell3, XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5, XivChatType.CrossLinkShell6, XivChatType.CrossLinkShell7, XivChatType.CrossLinkShell8,
    ];

    [SlashCommand("ls", "Send a /ls message in FFXIV, or set this channel as LS back-channel.")]
    public async Task LsAsync(
        [Summary("number", "Linkshell number (1–8)"), MinValue(1), MaxValue(8)] int number,
        [Summary("message", "Message text")] string? message = null)
        => await DispatchAsync(LsTypes[number - 1], message);

    [SlashCommand("kk", "Send a /ls message in FFXIV (German alias for /ls), or set this channel as LS back-channel.")]
    public async Task KkAsync(
        [Summary("number", "Linkshell number (1–8)"), MinValue(1), MaxValue(8)] int number,
        [Summary("message", "Message text")] string? message = null)
        => await DispatchAsync(LsTypes[number - 1], message);

    [SlashCommand("cwl", "Send a /cwl message in FFXIV, or set this channel as CWL back-channel.")]
    public async Task CwlAsync(
        [Summary("number", "Cross-World Linkshell number (1–8)"), MinValue(1), MaxValue(8)] int number,
        [Summary("message", "Message text")] string? message = null)
        => await DispatchAsync(CwlTypes[number - 1], message);

    [SlashCommand("wkk", "Send a /cwl message in FFXIV (German alias for /cwl), or set this channel as CWL back-channel.")]
    public async Task WkkAsync(
        [Summary("number", "Cross-World Linkshell number (1–8)"), MinValue(1), MaxValue(8)] int number,
        [Summary("message", "Message text")] string? message = null)
        => await DispatchAsync(CwlTypes[number - 1], message);

    [SlashCommand("tell", "Send a /tell to an FFXIV player.")]
    public async Task TellAsync(
        [Summary("character", "Character name (Firstname Lastname@World)"),
         Autocomplete(typeof(TellAutocompleteHandler))]
        string character,
        [Summary("message", "Message text")] string message)
    {
        if (!guard.CanSendTell(Context.User))
        {
            await RespondAsync(T("chat.no_permission_tell"), ephemeral: true);
            return;
        }
        var converted = messageConverter.ToGameText(message);
        var localMsg  = messageConverter.BuildLocalMessage(message);
        await SendChatAsync($"/tell {character} {converted}", XivChatType.TellOutgoing, converted, localMessage: localMsg);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private Task DispatchAsync(XivChatType chatType, string? message)
    {
        if (message is null) return SetBackChannelAsync(chatType);
        var gameCmd   = ChatTypeHelper.GetGameCommand(chatType)!;
        var converted = messageConverter.ToGameText(message);
        var localMsg  = messageConverter.BuildLocalMessage(message);
        return SendChatAsync($"{gameCmd} {converted}", chatType, converted, requireChat: true, localMsg);
    }

    private async Task SendChatAsync(string command, XivChatType expectedType, string expectedText,
                                     bool requireChat = false, SeString? localMessage = null)
    {
        if (requireChat && !guard.CanUseChatCommands(Context.User))
        {
            await RespondAsync(T("chat.no_permission_chat"), ephemeral: true);
            return;
        }

        // Register the confirmation BEFORE sending so we don't miss a fast response
        var confirmed = confirmations.WaitAsync(expectedType, expectedText, TimeSpan.FromSeconds(15));

        await framework.RunOnFrameworkThread(() =>
        {
            chatSender.Execute(command);
            if (localMessage is not null) chatGui.Print(localMessage);
        });

        await RespondAsync(T("chat.sent"), ephemeral: true);

        // Delete the ephemeral only when the matching message came back from FFXIV
        _ = DeleteWhenConfirmedAsync(confirmed);
    }

    private async Task SetBackChannelAsync(XivChatType chatType)
    {
        if (!guard.IsAdmin(Context.User.Id))
        {
            await RespondAsync(T("common.admin_only"), ephemeral: true);
            return;
        }

        if (Context.Guild is null)
        {
            await RespondAsync(T("chat.guild_channel_only"), ephemeral: true);
            return;
        }

        var config  = configStore.Load();
        var mapping = config.ChannelMappings.FirstOrDefault(m => m.DiscordChannelId == Context.Channel.Id);
        if (mapping is null)
        {
            mapping = new ChannelMapping { DiscordChannelId = Context.Channel.Id };
            config.ChannelMappings.Add(mapping);
        }

        mapping.BackChannelType = chatType;
        configStore.Save(config);

        await RespondAsync(T("chat.backchannel_set", ChatTypeHelper.GetFancyName(chatType)), ephemeral: true);
    }

    private async Task DeleteWhenConfirmedAsync(Task<bool> confirmed)
    {
        if (await confirmed)
            try { await DeleteOriginalResponseAsync(); } catch { }
    }
}

/// <summary>
/// Provides autocomplete suggestions for the /tell character parameter.
/// Sources (in priority order): recent tell partners, char links, online friends, online FC members.
/// Friends/FC are fetched live from game memory; silently skipped when unavailable.
/// </summary>
public sealed class TellAutocompleteHandler(IConfigStore configStore, SocialListService social)
    : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var input  = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;
        var config = configStore.Load();

        // Fetch friends and FC members concurrently; ignore errors when the game isn't loaded.
        IReadOnlyList<string> friends   = [];
        IReadOnlyList<string> fcMembers = [];
        try
        {
            var friendsTask   = social.GetFriendsAsync(onlineOnly: true);
            var fcMembersTask = social.GetFcMembersAsync(onlineOnly: true);
            await Task.WhenAll(friendsTask, fcMembersTask);
            friends   = friendsTask.Result;
            fcMembers = fcMembersTask.Result;
        }
        catch { /* proxy unavailable — continue without social data */ }

        var candidates = config.RecentTellPartners
            .Concat(config.CharLinks.Select(l => l.FfxivCharacter))
            .Concat(friends)
            .Concat(fcMembers)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(name => new AutocompleteResult(name, name));

        return AutocompletionResult.FromSuccess(candidates);
    }
}
