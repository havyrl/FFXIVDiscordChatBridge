using System.Collections.Concurrent;
using Dalamud.Plugin.Services;
using NetStone;
using NetStone.Search.Character;

namespace FFXIVDiscordBridgePlugin.Core;

/// <summary>
/// Fetches character avatar URLs from the Lodestone via the NetStone library and caches
/// them for the lifetime of the plugin.
/// Results (including "not found") are cached to avoid repeated HTTP calls for the same character.
/// </summary>
public sealed class CharacterAvatarService : IDisposable
{
    // Caches the Task itself so concurrent requests for the same character share one in-flight
    // HTTP fetch (CompletableFuture-style). Lazy<Task> ensures the factory runs exactly once
    // even when multiple threads race on GetOrAdd before the key is present.
    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> _cache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly IPluginLog _log;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private LodestoneClient? _lodestone;

    /// <summary>Fallback avatar URL used for system/non-character messages.</summary>
    public const string FallbackAvatarUrl =
        "https://raw.githubusercontent.com/goatcorp/DalamudAssets/master/UIRes/logo.png";

    public CharacterAvatarService(IPluginLog log) => _log = log;

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the Lodestone avatar URL for the given character, or <c>null</c> if not found.
    /// The first call for a character performs up to two HTTP requests; subsequent calls return
    /// the cached value.
    /// </summary>
    public Task<string?> GetAvatarUrlAsync(string characterName, string world)
    {
        var key = $"{characterName}@{world}";
        return _cache.GetOrAdd(key,
            k => new Lazy<Task<string?>>(
                () => FetchAvatarUrlAsync(characterName, world),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    // ── Private ────────────────────────────────────────────────────────────

    private async Task<LodestoneClient> GetClientAsync()
    {
        if (_lodestone is not null) return _lodestone;

        await _initLock.WaitAsync();
        try
        {
            _lodestone ??= await LodestoneClient.GetClientAsync();
            return _lodestone;
        }
        finally { _initLock.Release(); }
    }

    private async Task<string?> FetchAvatarUrlAsync(string characterName, string world)
    {
        try
        {
            var client = await GetClientAsync();

            _log.Debug("[AvatarService] Searching Lodestone for {Name}@{World}", characterName, world);

            var searchPage = await client.SearchCharacter(new CharacterSearchQuery
            {
                CharacterName = characterName,
                World         = world,
            });

            if (searchPage?.Results is null)
            {
                _log.Warning("[AvatarService] Null result from Lodestone search for {Name}@{World}", characterName, world);
                return null;
            }

            var entry = searchPage.Results
                .FirstOrDefault(r => string.Equals(r.Name, characterName, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                _log.Warning("[AvatarService] No matching character '{Name}' on '{World}' in Lodestone results", characterName, world);
                return null;
            }

            var character = await entry.GetCharacter();
            if (character is null)
            {
                _log.Warning("[AvatarService] Failed to fetch character details for {Name}@{World}", characterName, world);
                return null;
            }

            var avatarUrl = character.Avatar?.ToString();
            _log.Debug("[AvatarService] Found avatar for {Name}@{World}: {AvatarUrl}", characterName, world, avatarUrl ?? "(null)");
            return avatarUrl;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[AvatarService] Failed to fetch avatar for {Name}@{World}", characterName, world);
            return null;
        }
    }

    public void Dispose()
    {
        (_lodestone as IDisposable)?.Dispose();
        _initLock.Dispose();
    }
}
