using Dalamud.Plugin;
using FFXIVDiscordBridgePlugin.Core;

namespace FFXIVDiscordBridgePlugin.Config;

/// <summary>
/// Default IConfigStore implementation using Dalamud's built-in plugin config system.
/// Serialises to %AppData%\XIVLauncher\pluginConfigs\FFXIVDiscordBridgePlugin.json.
/// To use a different backend, implement IConfigStore and update the DI registration in Plugin.cs.
/// </summary>
public sealed class DalamudConfigStore(IDalamudPluginInterface pluginInterface) : IConfigStore
{
    public PluginConfig Load()
        => pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();

    public void Save(PluginConfig config)
        => pluginInterface.SavePluginConfig(config);
}
