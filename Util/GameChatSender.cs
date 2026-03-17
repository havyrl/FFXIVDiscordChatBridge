using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace FFXIVDiscordBridgePlugin.Util;

/// <summary>
/// Executes native game text commands (e.g. "/say Hello") via RaptureShellModule.
/// Must always be called on the Dalamud framework thread.
/// </summary>
public sealed class GameChatSender(IPluginLog log)
{
    public unsafe void Execute(string command)
    {
        var uiModule = UIModule.Instance();
        if (uiModule == null)
        {
            log.Warning("[GameChatSender] UIModule is null, cannot execute: {Cmd}", command);
            return;
        }

        var shell = uiModule->GetRaptureShellModule();
        if (shell == null)
        {
            log.Warning("[GameChatSender] RaptureShellModule is null, cannot execute: {Cmd}", command);
            return;
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(command + "\0");
        fixed (byte* ptr = bytes)
        {
            var str = new Utf8String();
            str.Ctor();
            str.SetString(ptr);
            shell->ExecuteCommandInner(&str, uiModule);
            str.Dtor();
        }
    }
}
