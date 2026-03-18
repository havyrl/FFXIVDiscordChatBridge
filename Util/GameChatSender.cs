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
        // FFXIV cannot render characters outside the BMP (codepoints > U+FFFF),
        // which includes most emoji — including those Discord auto-converts from emoticons like ":-)".
        // Strip all UTF-16 surrogate pairs before passing the text to the game.
        command = StripSurrogatePairs(command);
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

    private static string StripSurrogatePairs(string input)
    {
        if (!input.Any(char.IsHighSurrogate)) return input;

        var sb = new System.Text.StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            if (char.IsHighSurrogate(input[i]))
            {
                i++; // skip the paired low surrogate
                continue;
            }
            sb.Append(input[i]);
        }
        return sb.ToString();
    }
}
