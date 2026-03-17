using FFXIVDiscordBridgePlugin.Config;

namespace FFXIVDiscordBridgePlugin.Core;

/// <summary>
/// Abstraction over the config persistence layer.
/// Default implementation: DalamudConfigStore (Dalamud Plugin Config system).
/// To swap storage: implement this interface, change the DI registration in Plugin.cs.
/// </summary>
public interface IConfigStore
{
    PluginConfig Load();
    void Save(PluginConfig config);
}
