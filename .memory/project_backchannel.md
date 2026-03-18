---
name: Backchannel Discord→FFXIV
description: Wie Discord-Nachrichten ins Spiel kommen — zwei Einstiegspunkte, beide mit GameChatSender
type: project
---

Discord→FFXIV-Nachrichten laufen über zwei Einstiegspunkte, beide enden in `GameChatSender.Execute(string)`.

**Why:** Wichtig bei allen Features die eingehende Discord-Nachrichten transformieren müssen
(z.B. Item-URLs auflösen, Markdown entfernen).
**How to apply:** Jede Transformation von Discord-Text muss an BEIDEN Stellen eingebaut werden,
oder über einen zentralen `MessageConverter` (geplant) laufen.

---

## Einstiegspunkt A — Backchannel (automatisch)

**Datei:** `Discord/BotService.cs` → `OnMessageReceived()`

Wenn ein Discord-Kanal als Back-Channel konfiguriert ist (`ChannelMapping.BackChannelType`),
werden alle Nachrichten von echten Usern (nicht Bots/Webhooks) automatisch weitergeleitet:

```csharp
var text    = message.Content;   // ← roher Discord-Text, keine Transformation
var gameCmd = ChatTypeHelper.GetGameCommand(mapping.BackChannelType!.Value);
await _framework.RunOnFrameworkThread(() => _chatSender.Execute($"{gameCmd} {text}"));
```

## Einstiegspunkt B — Slash Commands

**Datei:** `Discord/Modules/ChatModule.cs`

`/say`, `/fc`, `/party`, `/yell`, `/shout`, `/ls`, `/cwl`, `/kk`, `/wkk`, `/tell` — alle
leiten den `message`-Parameter direkt an `GameChatSender.Execute()` weiter:

```csharp
var gameCmd = ChatTypeHelper.GetGameCommand(chatType)!;
chatSender.Execute($"{gameCmd} {message}");   // ← roher Discord-Text
```

---

## Wichtige Einschränkung

`GameChatSender` nutzt `RaptureShellModule->ExecuteCommandInner` — das entspricht manuell
getipptem Chat-Input. **Keine SeString-Injektion möglich.** Deshalb:

- Item-Links können Discord→FFXIV nur als `[Item Name]` (plain text) ankommen, nicht als klickbare Links
- Map-Links analog: nur als Text, kein klickbarer In-Game-Link
- Emoji / Markdown muss vor dem Execute-Aufruf bereinigt werden

---

## Confirmation-Service-Abhängigkeit

`ChatModule` nutzt `ChatConfirmationService`: erwartet dass der gesendete Text exakt im
Chat-Log zurückkommt. Wenn `MessageConverter.ToGameText()` den Text ändert, muss
`expectedText` den **konvertierten** String enthalten, nicht den Original-Discord-Text.
