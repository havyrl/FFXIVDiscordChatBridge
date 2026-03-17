using Discord.Interactions;

namespace FFXIVDiscordBridgePlugin.Util;

/// <summary>
/// Provides locale-specific descriptions for Discord slash commands and their parameters,
/// sourced from the plugin's embedded locale JSON files.
/// <para>
/// JSON keys follow the pattern <c>slash.{command}[.{subgroup}[.{subcommand}]][.{parameter}]</c>,
/// mirroring the hierarchical key list Discord.Net passes to this manager.
/// </para>
/// <para>
/// Parameter names are not localized (kept as-is from the <c>[Summary]</c> attribute).
/// The English description lives in the <c>[SlashCommand]</c>/<c>[Summary]</c> attribute and
/// serves as Discord's default; this manager only supplies the non-English overrides.
/// </para>
/// </summary>
public sealed class SlashCommandLocalizationManager(ILocalizer localizer) : ILocalizationManager
{
    /// <summary>Discord locale codes for which we supply translations.</summary>
    private static readonly string[] SupportedLocales = ["de"];

    public IDictionary<string, string>? GetAllDescriptions(IList<string> key, LocalizationTarget target)
    {
        var jsonKey = "slash." + string.Join(".", key);
        var result  = new Dictionary<string, string>();

        foreach (var locale in SupportedLocales)
        {
            var value = localizer.T(jsonKey, locale);
            // T() returns the key itself when no translation exists in any locale.
            // Since slash.* keys are intentionally absent from en.json, the fallback
            // chain terminates at "return key" — so this check is safe.
            if (value != jsonKey)
                result[locale] = value;
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Parameter names are not localized; Discord uses the <c>[Summary(name, …)]</c> value.
    /// </summary>
    public IDictionary<string, string>? GetAllNames(IList<string> key, LocalizationTarget target)
        => null;
}
