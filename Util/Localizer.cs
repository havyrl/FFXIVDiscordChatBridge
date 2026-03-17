using System.Reflection;
using System.Text.Json;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Discord.Interactions;

namespace FFXIVDiscordBridgePlugin.Util;

/// <summary>
/// Loads locale JSON files from embedded resources and resolves translated strings.
/// <para>
/// For Discord responses pass the Discord <c>UserLocale</c> string (e.g. "de", "en-US").<br/>
/// For ImGui/GUI strings omit the locale — the FFXIV client language is used instead.
/// </para>
/// </summary>
public interface ILocalizer
{
    /// <summary>Returns the translated string for <paramref name="key"/> using the FFXIV client language.</summary>
    string T(string key);

    /// <summary>Returns the translated string for <paramref name="key"/> using the given Discord locale (e.g. "de", "en-US").</summary>
    string T(string key, string? discordLocale);
}

public sealed class Localizer : ILocalizer
{
    private static readonly string[] SupportedLocales = ["en", "de", "ja", "fr"];

    private readonly IDataManager _dataManager;
    private readonly Dictionary<string, Dictionary<string, string>> _locales = new();

    public Localizer(IDataManager dataManager)
    {
        _dataManager = dataManager;

        var asm = Assembly.GetExecutingAssembly();
        foreach (var locale in SupportedLocales)
        {
            using var stream = asm.GetManifestResourceStream($"FFXIVDiscordBridgePlugin.Loc.{locale}.json");
            if (stream is null) continue;
            _locales[locale] = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
        }
    }

    public string T(string key) => Resolve(key, FfxivLocale());

    public string T(string key, string? discordLocale)
        => Resolve(key, discordLocale is null ? FfxivLocale() : MapDiscordLocale(discordLocale));

    private string Resolve(string key, string locale)
    {
        if (_locales.TryGetValue(locale, out var dict) && dict.TryGetValue(key, out var value))
            return value;
        if (_locales.TryGetValue("en", out var en) && en.TryGetValue(key, out var fallback))
            return fallback;
        return key;
    }

    private string FfxivLocale() => _dataManager.Language switch
    {
        ClientLanguage.German   => "de",
        ClientLanguage.Japanese => "ja",
        ClientLanguage.French   => "fr",
        _                       => "en",
    };

    private static string MapDiscordLocale(string locale) =>
        locale.StartsWith("de", StringComparison.Ordinal) ? "de" :
        locale.StartsWith("ja", StringComparison.Ordinal) ? "ja" :
        locale.StartsWith("fr", StringComparison.Ordinal) ? "fr" : "en";
}

/// <summary>
/// Base class for all Discord interaction modules.
/// Provides <c>T(key)</c> / <c>T(key, args)</c> helpers that resolve strings
/// using the Discord user's locale from the current interaction context.
/// </summary>
public abstract class LocalizedModuleBase(ILocalizer localizer)
    : InteractionModuleBase<SocketInteractionContext>
{
    protected ILocalizer Localizer { get; } = localizer;

    /// <summary>Returns the localized string for <paramref name="key"/> in the current user's Discord locale.</summary>
    protected string T(string key)
        => Localizer.T(key, Context.Interaction.UserLocale);

    /// <summary>Returns the localized string formatted with <paramref name="args"/>.</summary>
    protected string T(string key, params object[] args)
        => string.Format(Localizer.T(key, Context.Interaction.UserLocale), args);

    /// <summary>
    /// Responds with "admin only" and returns false if <paramref name="isAdmin"/> returns false.
    /// Usage: <c>if (!await RequireAdminAsync(guard.IsAdmin)) return;</c>
    /// </summary>
    protected async Task<bool> RequireAdminAsync(Func<ulong, bool> isAdmin)
    {
        if (isAdmin(Context.User.Id)) return true;
        await RespondAsync(T("common.admin_only"), ephemeral: true);
        return false;
    }
}
