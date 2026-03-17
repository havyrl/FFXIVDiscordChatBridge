using Dalamud.Plugin;
using FFXIVDiscordBridgePlugin.Core;

namespace FFXIVDiscordBridgePlugin.Config;

/// <summary>
/// Default IConfigStore implementation using Dalamud's built-in plugin config system.
/// Serialises to %AppData%\XIVLauncher\pluginConfigs\FFXIVDiscordBridgePlugin.json.
/// Config is cached in memory after the first load; the cache is updated on every Save()
/// so that repeated Load() calls on the game thread never hit the disk.
/// To use a different backend, implement IConfigStore and update the DI registration in Plugin.cs.
/// </summary>
public sealed class DalamudConfigStore(IDalamudPluginInterface pluginInterface) : IConfigStore
{
    private PluginConfig? _cached;

    public PluginConfig Load()
    {
        if (_cached is not null) return _cached;
        var config = pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        Migrate(config);
        return _cached = config;
    }

    /// <summary>
    /// One-time migrations applied after deserialization.
    /// Changes are written back to disk only when something was actually migrated.
    /// </summary>
    private void Migrate(PluginConfig config)
    {
        // Remove RecentTellPartners entries that have no "@World" suffix — they were
        // recorded before the fix that started storing "Name@World" for tell partners.
        var before = config.RecentTellPartners.Count;
        config.RecentTellPartners.RemoveAll(p => !p.Contains('@'));
        if (config.RecentTellPartners.Count != before)
            pluginInterface.SavePluginConfig(config);
    }

    public void Save(PluginConfig config)
    {
        pluginInterface.SavePluginConfig(config);
        _cached = config;
    }
}
