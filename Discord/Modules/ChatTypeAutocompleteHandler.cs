using Discord;
using Discord.Interactions;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Modules;

/// <summary>
/// Provides autocomplete suggestions for FFXIV chat type slugs.
/// Matches on slug prefix or fancy-name substring (case-insensitive), max 25 results.
/// </summary>
public sealed class ChatTypeAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var current = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        var results = ChatTypeHelper.All.Values
            .Where(info =>
                current.Length == 0
                || info.Slug.StartsWith(current, StringComparison.OrdinalIgnoreCase)
                || info.FancyName.Contains(current, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(info => new AutocompleteResult(info.FancyName, info.Slug));

        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }
}
