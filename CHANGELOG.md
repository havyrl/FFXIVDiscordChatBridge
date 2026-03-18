
### Bug Fixes
- Wrap repo.json in array (Dalamud custom repo format)

### Features
- Item links and map links forwarding via MessageConverter
- Delete original Discord message after back-channel forwarding
- Generate map pin image and attach to Discord message
- Localize chat type names and add /chattypes command
- Add MapLinkStyle config toggle (TextWithLink / ImageWithPin)

### Bug Fixes
- Log exceptions in slash command error handler
- Read FFXIV client state on framework thread in InfoModule
- Unsubscribe UI events on dispose and wire OpenMainUi

### Features
- Map FFXIV private-use Unicode characters to Discord text
- Resolve character avatar URLs via XIVAPI
- Set bot presence based on player login state
- Suppress duplicate webhook messages within a time window
- Notify Discord when the Duty Finder queue pops
- Notify Discord when a retainer sells an item
- Wire all new features into config and DI
- Dalamud config persistence via DalamudConfigStore
- Admin request bridge service for in-game approval popup
- Add localization system (en/de) via embedded JSON
- Add /config channel info command
- Embed build timestamp and print load notification in chat
- Switch CharacterAvatarService to NetStone for Lodestone lookups
- Add infrastructure services and ChatTypeHelper extensions
- Add WebhookResolver for automatic webhook provisioning
- Extend DiscordMessagePayload and WebhookSender with embed/component/DM support
- Extend PluginConfig model and add in-memory caching to DalamudConfigStore
- Add duty finder rich embed notification with Join/Toggle buttons
- Add party invite detection with Accept/Decline Discord interactions
- Add /friendlist and /fclist slash commands
- Add back-channel support, LS/CWL/NN commands, and delivery confirmation to ChatModule
- Update /config with autocomplete, duty/party subcommands, and admin helper
- Add guild-specific slash command registration and primary guild UI
- Wire all new services and handlers into DI container

### Localization
- Add translations for duty finder, party invite, social, and back-channel features

