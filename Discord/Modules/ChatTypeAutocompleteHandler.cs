using Discord;
using Discord.Interactions;
using FFXIVDiscordBridgePlugin.Util;
using Microsoft.Extensions.DependencyInjection;

namespace FFXIVDiscordBridgePlugin.Discord.Modules;

/// <summary>
/// Provides autocomplete suggestions for FFXIV chat type slugs.
/// Matches on slug prefix or localized-name substring (case-insensitive), max 25 results.
/// Display names are localized using the Discord user's locale.
/// </summary>
public sealed class ChatTypeAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var localizer = services.GetRequiredService<ILocalizer>();
        var locale    = autocompleteInteraction.UserLocale;
        var current   = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        var results = ChatTypeHelper.All
            .Select(kvp => new
            {
                kvp.Value.Slug,
                Name = ChatTypeHelper.GetLocalizedName(kvp.Key, localizer, locale),
            })
            .Where(x =>
                current.Length == 0
                || x.Slug.StartsWith(current, StringComparison.OrdinalIgnoreCase)
                || x.Name.Contains(current, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(x => new AutocompleteResult(x.Name, x.Slug));

        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }
}
