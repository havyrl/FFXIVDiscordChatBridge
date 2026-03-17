using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using Dalamud.Plugin.Services;

namespace FFXIVDiscordBridgePlugin.Core;

/// <summary>
/// Fetches character avatar URLs from XIVAPI and caches them for the lifetime of the plugin.
/// Results (including "not found") are cached to avoid repeated HTTP calls for the same character.
/// </summary>
public sealed class CharacterAvatarService : IDisposable
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    private readonly ConcurrentDictionary<string, string?> _cache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly IPluginLog _log;

    public CharacterAvatarService(IPluginLog log) => _log = log;

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the Lodestone avatar URL for the given character, or <c>null</c> if not found.
    /// The first call for a character performs an HTTP request; subsequent calls return the cached value.
    /// </summary>
    public async Task<string?> GetAvatarUrlAsync(string characterName, string world)
    {
        var key = $"{characterName}@{world}";
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var avatarUrl = await FetchAvatarUrlAsync(characterName, world);
        _cache[key] = avatarUrl; // cache null as negative result
        return avatarUrl;
    }

    // ── Private ────────────────────────────────────────────────────────────

    private async Task<string?> FetchAvatarUrlAsync(string characterName, string world)
    {
        try
        {
            var encodedName = Uri.EscapeDataString(characterName);
            var url = $"https://xivapi.com/character/search?name={encodedName}&server={world}";
            var json = await Http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Results", out var results))
                return null;

            foreach (var result in results.EnumerateArray())
            {
                if (!result.TryGetProperty("Name", out var nameProp)) continue;
                if (!nameProp.GetString()?.Equals(characterName, StringComparison.OrdinalIgnoreCase) == true) continue;
                if (result.TryGetProperty("Avatar", out var avatarProp))
                    return avatarProp.GetString();
            }

            _log.Debug("[AvatarService] No result for {Name}@{World}", characterName, world);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[AvatarService] Failed to fetch avatar for {Name}@{World}", characterName, world);
        }

        return null;
    }

    public void Dispose() { /* Http is static — not disposed here */ }
}
