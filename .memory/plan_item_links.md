---
name: Plan Item Links
description: Implementierungsplan für Item-Link-Feature (FFXIV↔Discord), zweistufig
type: project
---

Item-Links im FFXIV-Chat als klickbare URLs nach Discord weiterleiten (Stufe 1),
und Teamcraft-Item-URLs in Discord-Nachrichten als lesbaren Text ins Spiel zurückbringen (Stufe 2).

**Why:** Item-Links gehen mit `message.TextValue` verloren. Zentraler Converter verarbeitet
beide Richtungen konsistent.
**How to apply:** Beide Stufen über einen gemeinsamen `MessageConverter` leiten.
Stufe 1 zuerst. Stufe 2 nutzt denselben Converter, andere Richtung.

---

## Zentraler Converter: `Util/MessageConverter.cs`

Ein Service der ALLE Text-Transformationen für beide Richtungen kapselt:

**Richtung FFXIV→Discord** (`ToDiscord(SeString) → string`):
- Iteriert `SeString.Payloads`
- `ItemPayload` → `[Name](Teamcraft-URL)` (oder konfigurierte DB)
- `MapLinkPayload` → `📍 [Zone (X, Y)](Teamcraft-Map-URL)` (Stufe 1 der Map-Pläne)
- `TextPayload` → `SpecialCharsHandler.Transform(text)` (ersetzt bisherigen Aufruf)
- Alle anderen Payloads → `.TextValue`

**Richtung Discord→FFXIV** (`ToGameText(string) → string`):
- Regex: Teamcraft-Item-URL → `[Item Name]` (Lumina-Lookup, siehe unten)
- Regex: Discord-Mentions `<@123456>` → Benutzername (optional, späteres Feature)
- Discord-Markdown entfernen: `**fett**` → `fett`, `__unterstrichen__` → `unterstrichen`
- Gibt spielkompatiblen String zurück (kein SeString — GameChatSender nimmt nur Text)

> **Wichtig:** Discord→FFXIV kann keine klickbaren Item-Links erzeugen.
> `GameChatSender.Execute()` schickt Text-Commands an `RaptureShellModule`.
> Das Spiel parsed diese wie manuell getippten Chat — keine SeString-Injektion.
> Teamcraft-URL wird deshalb zu `[Eisenerz]` (plain text), nicht zu einem klickbaren Link.

---

## Stufe 1 — FFXIV → Discord: Item-Link als Teamcraft-URL

### Konkrete Änderungen

#### 1. `Util/MessageConverter.cs` — neu

```csharp
public sealed class MessageConverter(SpecialCharsHandler specialChars,
                                      IConfigStore configStore,
                                      IDataManager dataManager,
                                      IPluginLog log)
{
    // FFXIV→Discord
    public string ToDiscord(SeString message) { ... }

    // Discord→FFXIV
    public string ToGameText(string discordText) { ... }

    private string BuildItemUrl(uint itemId) { ... }    // Teamcraft/GarlandTools/Custom
    private string LookupItemName(uint itemId) { ... }  // Lumina Item-Sheet
}
```

#### 2. Payload-Iteration in `ToDiscord()`

Payload-Sequenz für Item-Links in FFXIV SeStrings:
```
ItemPayload          ← enthält ItemId, IsHQ
UIForegroundPayload  ← Farbe (ignorieren)
UIGlowPayload        ← Glow (ignorieren)
TextPayload          ← Item-Name (z.B. "Eisenerz")
UIForegroundPayload  ← Ende-Farbe (ignorieren)
UIGlowPayload        ← Ende-Glow (ignorieren)
RawPayload           ← Ende des Links
```

Strategie: State-Machine über Payloads.
- Bei `ItemPayload`: ItemId + IsHQ merken, `insideItemLink = true`
- Bei `TextPayload` wenn `insideItemLink`: Name sammeln
- Bei nächstem `ItemPayload`-Ende (RawPayload oder nächstem ItemPayload): Link ausgeben

Alternativ (einfacher): Alle Payloads mit Index iterieren, bei `ItemPayload[i]`
den ersten folgenden `TextPayload` als Namen verwenden.

#### 3. `Chat/ChatEventSource.cs` — anpassen

```csharp
// Vorher:
var content = $"[{slug}] {_specialChars.Transform(rawText)}";

// Nachher (rawText durch SeString ersetzen):
// OnChatMessage: message-SeString vor await-Grenze kopieren/festhalten
var content = $"[{slug}] {_messageConverter.ToDiscord(message)}";
```

> **Achtung:** `message` ist ein `ref SeString` im Game-Thread. Vor dem `await` muss
> die Konvertierung vollständig abgeschlossen sein oder die Payloads müssen kopiert werden.
> Aktuell wird `message.TextValue` vor `await` gecaptured — genauso mit `ToDiscord()`.

#### 4. Config-Erweiterung

```csharp
public enum ItemLinkDatabase { Teamcraft, GarlandTools, Custom }

// In PluginConfig:
public ItemLinkDatabase ItemDatabase { get; set; } = ItemLinkDatabase.Teamcraft;
public string CustomItemUrlTemplate { get; set; } = "";
public string ItemLinkLocale { get; set; } = "de"; // en/de/fr/ja
```

URL-Templates:
- Teamcraft: `https://ffxivteamcraft.com/db/{locale}/item/{id}`
- GarlandTools: `https://www.garlandtools.org/db/#item/{id}` (keine Locale)
- Custom: `{id}`-Platzhalter frei konfigurierbar

#### 5. Betroffene Dateien (Stufe 1)

| Datei | Änderung |
|---|---|
| `Util/MessageConverter.cs` | neu — ToDiscord + ToGameText |
| `Chat/ChatEventSource.cs` | `_specialChars.Transform(rawText)` → `_converter.ToDiscord(message)` |
| `Config/PluginConfig.cs` | ItemLinkDatabase, CustomItemUrlTemplate, ItemLinkLocale |
| `Gui/MainWindow.cs` | Config-UI: Datenbank-Auswahl, Locale |
| `Plugin.cs` | DI-Registrierung MessageConverter |
| Locale `en.json`, `de.json` | UI-Strings für Config-Felder |

---

## Stufe 2 — Discord → FFXIV: Teamcraft-URL als lesbarer Item-Name

### Ablauf

`MessageConverter.ToGameText()` wird an beiden Discord→FFXIV Einstiegspunkten aufgerufen:

**Einstiegspunkt A — Backchannel** (`BotService.OnMessageReceived`):
```csharp
// Vorher:
var text = message.Content;
// Nachher:
var text = _messageConverter.ToGameText(message.Content);
await _framework.RunOnFrameworkThread(() => _chatSender.Execute($"{gameCmd} {text}"));
```

**Einstiegspunkt B — Slash Commands** (`ChatModule`):
```csharp
// Vorher:
return SendChatAsync($"{gameCmd} {message}", chatType, message, requireChat: true);
// Nachher:
var converted = _messageConverter.ToGameText(message);
return SendChatAsync($"{gameCmd} {converted}", chatType, converted, requireChat: true);
```

> **Hinweis:** `ChatConfirmationService` vergleicht den erwarteten Text mit dem zurückgemeldeten
> Chat-Text. Wenn der Text durch den Converter geändert wird, muss `expectedText` auf den
> konvertierten String zeigen — sonst schlägt die Confirmation-Logik fehl.

### Teamcraft-URL → Item-Name (Lumina-Lookup)

```csharp
// Regex: https://ffxivteamcraft.com/db/\w+/item/(\d+)
private static readonly Regex TeamcraftItemRegex =
    new(@"https://ffxivteamcraft\.com/db/\w+/item/(\d+)(?:/[^\s]*)?", RegexOptions.Compiled);

// In ToGameText():
text = TeamcraftItemRegex.Replace(text, match => {
    if (uint.TryParse(match.Groups[1].Value, out var itemId))
    {
        var name = dataManager.GetExcelSheet<Item>()?.GetRow(itemId)?.Name.ToString();
        return name is not null ? $"[{name}]" : match.Value; // fallback: URL behalten
    }
    return match.Value;
});
```

### Betroffene Dateien (Stufe 2)

| Datei | Änderung |
|---|---|
| `Util/MessageConverter.cs` | ToGameText() mit Teamcraft-Regex + Lumina-Lookup |
| `Discord/BotService.cs` | `_messageConverter` injizieren, `ToGameText()` vor Execute |
| `Discord/Modules/ChatModule.cs` | `_messageConverter` injizieren, `ToGameText()` vor Execute + Confirmation-Fix |

---

## Offene Fragen

- HQ-Items: `ItemPayload.IsHQ` flag korrekt im Link-Text kennzeichnen? (z.B. `[Eisenerz ❇]`)
- Kollektables / EventItems / andere `ItemKindType` — separat behandeln oder ignorieren?
- GarlandTools-ID = Lumina-ItemId? (vermutlich ja, aber verifizieren)
