using Dalamud.Plugin.Services;
using Discord;
using Discord.Interactions;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Modules;

/// <summary>
/// Direct FFXIV chat commands: /say, /fc, /party, /yell, /shout, /tell
/// All commands require the user to be on the whitelist (CanUseChatCommands),
/// except /tell which requires CanSendTell.
/// </summary>
public sealed class ChatModule(ILocalizer localizer, PermissionGuard guard, ICommandManager commandManager,
                               IConfigStore configStore, IFramework framework)
    : LocalizedModuleBase(localizer)
{
    [SlashCommand("say", "Send a /say message in FFXIV.")]
    public async Task SayAsync([Summary("message", "Message text")] string message)
        => await SendChatAsync($"/say {message}", requireChat: true);

    [SlashCommand("yell", "Send a /yell message in FFXIV.")]
    public async Task YellAsync([Summary("message", "Message text")] string message)
        => await SendChatAsync($"/yell {message}", requireChat: true);

    [SlashCommand("shout", "Send a /shout message in FFXIV.")]
    public async Task ShoutAsync([Summary("message", "Message text")] string message)
        => await SendChatAsync($"/shout {message}", requireChat: true);

    [SlashCommand("fc", "Send a /fc message in FFXIV.")]
    public async Task FcAsync([Summary("message", "Message text")] string message)
        => await SendChatAsync($"/fc {message}", requireChat: true);

    [SlashCommand("party", "Send a /party message in FFXIV.")]
    public async Task PartyAsync([Summary("message", "Message text")] string message)
        => await SendChatAsync($"/p {message}", requireChat: true);

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
        await SendChatAsync($"/tell {character} {message}", requireChat: false);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task SendChatAsync(string command, bool requireChat)
    {
        if (requireChat && !guard.CanUseChatCommands(Context.User))
        {
            await RespondAsync(T("chat.no_permission_chat"), ephemeral: true);
            return;
        }

        // Dalamud API must be called on the framework thread
        await framework.RunOnFrameworkThread(() =>
        {
            commandManager.ProcessCommand(command);
        });

        await RespondAsync(T("chat.sent"), ephemeral: true);
    }
}

/// <summary>
/// Provides autocomplete suggestions for the /tell character parameter.
/// Sources: recent tell partners, then char links.
/// </summary>
public sealed class TellAutocompleteHandler(IConfigStore configStore)
    : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var input  = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;
        var config = configStore.Load();

        var candidates = config.RecentTellPartners
            .Concat(config.CharLinks.Select(l => l.FfxivCharacter))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(name => new AutocompleteResult(name, name));

        return Task.FromResult(AutocompletionResult.FromSuccess(candidates));
    }
}
