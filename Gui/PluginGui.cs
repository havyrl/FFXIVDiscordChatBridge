using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace FFXIVDiscordBridgePlugin.Gui;

/// <summary>
/// Registers the WindowSystem and the in-game chat command /discordbridge.
/// </summary>
public sealed class PluginGui : IDisposable
{
    private readonly WindowSystem          _windowSystem = new("FFXIVDiscordBridgePlugin");
    private readonly MainWindow            _mainWindow;
    private readonly ICommandManager       _commandManager;
    private readonly IDalamudPluginInterface _pluginInterface;

    private const string Command = "/discordbridge";

    public PluginGui(MainWindow mainWindow, AdminRequestWindow adminRequestWindow,
                     ICommandManager commandManager, IDalamudPluginInterface pluginInterface)
    {
        _mainWindow      = mainWindow;
        _commandManager  = commandManager;
        _pluginInterface = pluginInterface;

        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(adminRequestWindow);

        pluginInterface.UiBuilder.Draw         += _windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += OpenMainWindow;
        pluginInterface.UiBuilder.OpenMainUi   += OpenMainWindow;

        _commandManager.AddHandler(Command, new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Open the FFXIV Discord Bridge configuration window.",
        });
    }

    private void OnCommand(string command, string args) => OpenMainWindow();

    private void OpenMainWindow()
    {
        _mainWindow.IsOpen = true;
        _mainWindow.BringToFront();
    }

    public void Dispose()
    {
        _pluginInterface.UiBuilder.Draw         -= _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenConfigUi -= OpenMainWindow;
        _pluginInterface.UiBuilder.OpenMainUi   -= OpenMainWindow;
        _commandManager.RemoveHandler(Command);
        _windowSystem.RemoveAllWindows();
    }
}
