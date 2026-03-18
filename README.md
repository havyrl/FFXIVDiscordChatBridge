# FFXIV Discord Bridge

**FFXIV Discord Bridge** is a [Dalamud](https://github.com/goatcorp/Dalamud) plugin that connects your Final Fantasy XIV in-game chat with a Discord server — in both directions.

Chat messages from FFXIV are automatically forwarded to Discord channels of your choice. From Discord, whitelisted users can reply directly back into the game. No extra software or accounts needed beyond a Discord bot token.

## Features

- **FFXIV → Discord:** Forwards chat messages from configurable FFXIV channels (say, tell, FC, party, …) to Discord channels via webhooks. Messages are posted with the character's name and Lodestone avatar.
- **Discord → FFXIV:** Sends messages from Discord back to the game via slash commands (whitelisted users only).
- **Duty Finder notifications:** Posts a notification when a Duty Finder queue pops.
- **Retainer Sale notifications:** Posts a notification (with item icon) when a retainer completes a sale.
- **Per-channel permissions:** Whitelist Discord users/roles with fine-grained permissions (send messages, use `/tell`, use chat commands, view status).
- **Duplicate filter:** Suppresses identical messages within a configurable time window.
- **Admin DM support:** The designated bridge admin can receive notifications via direct message instead of a guild channel.

## Requirements

- Final Fantasy XIV (retail)
- [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher) with Dalamud enabled (API Level 14)
- A Discord bot token ([how to create one](https://discord.com/developers/applications))
- At least one Discord webhook URL for FFXIV → Discord forwarding

## Installation

> **Note:** This plugin is not in the official Dalamud plugin list. You need to add it as a custom repository.

1. Open XIVLauncher settings → **Experimental** → **Custom Plugin Repositories**.
2. Add the following URL and click **Save**:
   ```
   https://raw.githubusercontent.com/havyrl/FFXIVDiscordChatBridge/main/repo.json
   ```
3. Open the Dalamud Plugin Installer, search for **FFXIV Discord Bridge** and install it.
4. Open plugin settings via `/discordbridge` or the Dalamud plugin menu and enter your bot token to get started.

## Configuration

All settings are stored by Dalamud in `%AppData%\XIVLauncher\pluginConfigs\FFXIVDiscordBridgePlugin.json`.

### Minimal setup

| Field | Description |
|---|---|
| **Bot Token** | Discord bot token. The bot must be in your server with message permissions. |
| **Admin Discord User ID** | Your Discord user ID. Grants unrestricted access to all slash commands. |
| **Channel Mappings** | Each mapping links one or more FFXIV chat types to a Discord channel (via webhook). Optionally enable a back-channel to send Discord messages back to FFXIV. |

### Slash commands (Discord)

| Command | Description |
|---|---|
| `/tell <character> <message>` | Send a /tell in-game |
| `/say <message>` | Send a /say message |
| `/fc <message>` | Send a Free Company message |
| `/party <message>` | Send a party message |
| `/status` | Show bridge status |
| `/who` | Show the logged-in character |
| `/config …` | Manage whitelist and character links |

### Permissions

Discord users/roles must be whitelisted to interact with the bot. Each entry can grant:
- `CanSendToBackChannel` — post via back-channel
- `CanSendTell` — use `/tell`
- `CanUseChatCommands` — use `/say`, `/fc`, `/party`, etc.
- `CanViewStatus` — use `/status`, `/who` (granted by default)

## Development

### Prerequisites

- .NET 10 SDK
- XIVLauncher installed (provides Dalamud assemblies via the SDK)

### Build

```bash
dotnet build
```

The `Dalamud.NET.Sdk` NuGet package resolves Dalamud dependencies automatically.

### Project structure

```
FFXIVDiscordBridgePlugin/
  Chat/          — IGameEventSource implementations (chat, duty finder, retainer sales)
  Config/        — PluginConfig and DalamudConfigStore
  Core/          — Shared interfaces (IConfigStore, IGameEventSource, IDiscordActionHandler)
  Discord/
    Modules/     — Discord.Net slash command modules
    Interactions/ — IDiscordActionHandler implementations (e.g. TellReplyActionHandler)
  Gui/           — ImGui windows (MainWindow, AdminRequestWindow)
  Util/          — SpecialCharsHandler, CharacterAvatarService, …
  Plugin.cs      — Dalamud entry point / DI root
```

## Credits

The idea for this plugin was sparked by the outdated [Dalamud.DiscordBridge](https://github.com/reiichi001/Dalamud.DiscordBridge).

## License

MIT — see [LICENSE](LICENSE).
