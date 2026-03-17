using Dalamud.Plugin.Services;
using Discord;
using Discord.WebSocket;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Interactions;

/// <summary>
/// IDiscordActionHandler for the "Reply" button that appears on incoming Tell messages.
/// Custom ID schema: bridge:tell:reply:&lt;urlEncodedCharacterName&gt;
///
/// Flow:
///   1. User clicks Reply → bot shows a modal with a text field
///   2. User submits modal → handler sends /tell &lt;character&gt; &lt;message&gt; via ICommandManager
/// </summary>
public sealed class TellReplyActionHandler(ILocalizer localizer, PermissionGuard guard,
                                           ICommandManager commandManager, IFramework framework)
    : IDiscordActionHandler
{
    private const string ButtonPrefix = "bridge:tell:reply:";
    private const string ModalPrefix  = "bridge:tell:modal:";

    public bool CanHandle(string customId)
        => customId.StartsWith(ButtonPrefix) || customId.StartsWith(ModalPrefix);

    public async Task HandleAsync(SocketInteraction interaction)
    {
        if (!guard.CanSendTell(interaction.User))
        {
            await interaction.RespondAsync(localizer.T("tell.no_permission", interaction.UserLocale), ephemeral: true);
            return;
        }

        switch (interaction)
        {
            case SocketMessageComponent button when button.Data.CustomId.StartsWith(ButtonPrefix):
                await HandleButtonAsync(button);
                break;

            case SocketModal modal when modal.Data.CustomId.StartsWith(ModalPrefix):
                await HandleModalAsync(modal);
                break;

            default:
                await interaction.RespondAsync(localizer.T("tell.unrecognised", interaction.UserLocale), ephemeral: true);
                break;
        }
    }

    // ── Button → open modal ────────────────────────────────────────────────

    private async Task HandleButtonAsync(SocketMessageComponent button)
    {
        var encoded   = button.Data.CustomId[ButtonPrefix.Length..];
        var character = Uri.UnescapeDataString(encoded);
        var locale    = button.UserLocale;
        var modalId   = $"{ModalPrefix}{encoded}";

        var modal = new ModalBuilder()
            .WithTitle(string.Format(localizer.T("tell.reply_title", locale), character))
            .WithCustomId(modalId)
            .AddTextInput(localizer.T("tell.message_label", locale), "message", TextInputStyle.Paragraph,
                          placeholder: localizer.T("tell.message_placeholder", locale),
                          required: true, maxLength: 500)
            .Build();

        await button.RespondWithModalAsync(modal);
    }

    // ── Modal submit → send tell ───────────────────────────────────────────

    private async Task HandleModalAsync(SocketModal modal)
    {
        var encoded   = modal.Data.CustomId[ModalPrefix.Length..];
        var character = Uri.UnescapeDataString(encoded);
        var message   = modal.Data.Components
                             .FirstOrDefault(c => c.CustomId == "message")?.Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(message))
        {
            await modal.RespondAsync(localizer.T("tell.empty_message", modal.UserLocale), ephemeral: true);
            return;
        }

        await framework.RunOnFrameworkThread(() =>
        {
            commandManager.ProcessCommand($"/tell {character} {message}");
        });

        await modal.RespondAsync(
            string.Format(localizer.T("tell.sent", modal.UserLocale), character),
            ephemeral: true);
    }
}
