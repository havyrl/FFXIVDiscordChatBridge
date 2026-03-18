---
name: Plan Map Links
description: Implementierungsplan für Map-Link-Feature (FFXIV→Discord), zweistufig
type: project
---

Map-Links im FFXIV-Chat lesbar und klickbar nach Discord weiterleiten (Stufe 1),
und optional als Kartenbild mit Pin-Marker (Stufe 2).

**Why:** `MapLinkPayload` geht mit `message.TextValue` verloren. Stufe 1 ist Teil des
zentralen `MessageConverter` (siehe plan_item_links.md). Stufe 2 ist optionales Feature.
**How to apply:** Stufe 1 immer im MessageConverter implementieren. Stufe 2 per Config-Toggle.

---

## MapLinkPayload API (verifiziert, 2026-03-17)

Aus PandorasBox-Referenz (`AutoOpenCoords.cs`, `PreserveMapLinks.cs`) bestätigt:

| Property | Typ | Öffentlich? | Beschreibung |
|---|---|---|---|
| `XCoord` | `float` | ✅ public | Angezeigte X-Koordinate (z.B. 23.5) |
| `YCoord` | `float` | ✅ public | Angezeigte Y-Koordinate (z.B. 14.2) |
| `RawX` | `int` | ✅ public | Rohe X-Position × 1000 |
| `RawY` | `int` | ✅ public | Rohe Y-Position × 1000 |
| `PlaceName` | `string` | ✅ public | Zonenname (z.B. "Mor Dhona") |
| `TerritoryType` | `LazyRow<TerritoryType>` | ✅ public | Gebiet-Daten |
| `mapId` | `uint` | ❌ private | Nur per Reflection (nicht nötig für URL) |
| `territoryTypeId` | `uint` | ❌ private | Nur per Reflection (nicht nötig für URL) |

**Map.RowId für Teamcraft-URL:**
```csharp
var mapRowId = payload.TerritoryType.Value?.Map.Value?.RowId;
// Alternativ direkt (falls Map als LazyRow öffentlich):
// var mapRowId = payload.Map.RowId;
```

**Teamcraft-URL verifiziert:** `https://ffxivteamcraft.com/db/de/map/25` = Mor Dhona.
ID allein reicht (Name-Slug ist optional/SEO). Map.RowId == Teamcraft-ID bestätigt.

---

## Stufe 1 — FFXIV → Discord: Map-Link als Text + Teamcraft-URL

### Implementierung in `MessageConverter.ToDiscord()`

Teil des zentralen `MessageConverter` (siehe plan_item_links.md).

```csharp
case MapLinkPayload map:
    var mapId   = map.TerritoryType.Value?.Map.Value?.RowId;
    var zone    = map.PlaceName;
    var x       = map.XCoord.ToString("F1");
    var y       = map.YCoord.ToString("F1");
    var locale  = config.ItemLinkLocale; // "de" / "en" / etc.
    var url     = mapId.HasValue
        ? $"https://ffxivteamcraft.com/db/{locale}/map/{mapId}"
        : null;
    sb.Append(url is not null
        ? $"📍 [{zone} ({x}, {y})]({url})"
        : $"📍 {zone} ({x}, {y})");
    break;
```

### Betroffene Dateien (Stufe 1)

Nur `Util/MessageConverter.cs` — kein weiterer Aufwand da im Item-Stufe-1-Plan bereits enthalten.

---

## Stufe 2 — FFXIV → Discord: Kartenbild mit Pin

### Ziel

Wenn `MapLinkStyle = ImageWithPin`: PNG mit rotem Pin-Marker als Discord-Attachment senden,
statt (oder zusätzlich zu) dem Text-Link.

### Koordinaten-Umrechnung

```csharp
// MapLinkPayload.XCoord/YCoord (angezeigte Werte) → Pixelposition
// PandorasBox-Formel (GenerateRawPosition umgekehrt):
var map = payload.TerritoryType.Value!.Map.Value!;
float scale  = map.SizeFactor / 100f;
float pixelX = (payload.XCoord - 1f) * scale / 41f * 2048f;
float pixelY = (payload.YCoord - 1f) * scale / 41f * 2048f;
// Offset: pixelX += map.OffsetX * scale + 1024f (je nach Dalamud-Version verifizieren)
```

Map-Textur ist 2048×2048 für große Zonen, kann für kleinere Instanz-Maps abweichen.

### Map-Textur aus Spieldaten

```csharp
// Map.Id aus Lumina: z.B. "mor_dhona/00" → Pfad: "ui/map/mor_dhona/00/mor_dhona_00m.tex"
var mapId = map.Id.ToString().Replace("/", "_");
var path  = $"ui/map/{map.Id}/{mapId}m.tex";
var tex   = dataManager.GetFile<TexFile>(path);
// tex.GetRgbaImageData() → byte[] RGBA
```

Keine HTTP-Anfrage nötig. Texturformat-Abstraktion durch Dalamud's `TexFile`.

### Bild-Rendering mit `System.Drawing.Common`

Kein neues NuGet-Paket nötig (Windows-only, passt zu unserem Constraint):

```csharp
using var bmp   = new Bitmap(2048, 2048, PixelFormat.Format32bppArgb);
// RGBA-Daten in Bitmap kopieren (BitmapData / Marshal.Copy)
using var g     = Graphics.FromImage(bmp);
g.FillEllipse(Brushes.Red, pixelX - 10, pixelY - 10, 20, 20);
g.DrawEllipse(new Pen(Color.White, 2), pixelX - 10, pixelY - 10, 20, 20);
using var stream = new MemoryStream();
bmp.Save(stream, ImageFormat.Png);
stream.Seek(0, SeekOrigin.Begin);
```

> SkiaSharp wäre Overkill — `System.Drawing.Common` reicht vollständig.

### Neuer Service: `Util/MapImageService.cs`

```csharp
public sealed class MapImageService(IDataManager dataManager, IPluginLog log)
{
    private readonly Dictionary<uint, byte[]> _textureCache = new();

    public Stream? GenerateMapPin(MapLinkPayload payload)
    {
        // 1. Map-Daten aus Payload
        // 2. Textur laden + cachen
        // 3. Pixel-Koordinaten berechnen
        // 4. Pin zeichnen
        // 5. PNG-Stream zurückgeben (null bei Fehler → Fallback auf Text)
    }
}
```

### `WebhookSender` — Änderungen für Attachments

`DiscordWebhookClient` hat `SendFileAsync()` — separater Methodenaufruf:

```csharp
// In WebhookSender.SendAsync():
if (payload.MapImageStream is not null)
{
    await using (payload.MapImageStream)
    {
        await client.SendFileAsync(
            payload.MapImageStream,
            filename: "map.png",
            text: payload.Content,      // Text-Fallback als Caption
            username: payload.Username,
            avatarUrl: payload.AvatarUrl);
    }
}
else
{
    await client.SendMessageAsync(
        text: payload.Content, ...);
}
```

`DiscordMessagePayload` bekommt ein neues Feld:
```csharp
public Stream? MapImageStream { get; init; }
```

> **DM-Kanal:** `dmChannel.SendFileAsync(stream, "map.png", text: caption)` — Discord.Net
> unterstützt das auf `IDMChannel`. `SendDmAsync` in `WebhookSender` entsprechend erweitern.

> **Duplicate-Check:** Key-Berechnung in `WebhookSender` nutzt `payload.Content`.
> Bei Bild-Nachrichten ist Content leer oder nur Zone+Koords — das reicht als Dedup-Key.

### Config-Erweiterung

```csharp
public enum MapLinkStyle { TextOnly, TextWithLink, ImageWithPin }

// In PluginConfig:
public MapLinkStyle MapStyle { get; set; } = MapLinkStyle.TextWithLink;
```

### Betroffene Dateien (Stufe 2)

| Datei | Änderung |
|---|---|
| `Util/MapImageService.cs` | neu — Textur laden, Pin zeichnen, Stream zurückgeben |
| `Core/DiscordMessagePayload.cs` | `Stream? MapImageStream` hinzufügen |
| `Discord/WebhookSender.cs` | `SendFileAsync` wenn `MapImageStream != null` (Webhook + DM) |
| `Chat/ChatEventSource.cs` | `MapImageService` injizieren, bei `ImageWithPin` Payload füllen |
| `Config/PluginConfig.cs` | `MapLinkStyle` Enum + Feld |
| `Gui/MainWindow.cs` | Config-UI: Style-Auswahl |
| `Plugin.cs` | DI-Registrierung MapImageService |
| Locale `en.json`, `de.json` | UI-Strings für Style-Auswahl |

---

## Offene Fragen / Risiken (Stufe 2)

- Pixel-Koordinatenformel muss in der Praxis getestet werden (Offset-Term unklar)
- Map-Dateipfad-Schema: `map.Id.ToString()` Format muss verifiziert werden
- Instanz-Maps (Dungeons): `mapId | (instance << 16)` — RowId bleibt aber dieselbe, Pin-Position korrekt?
- `System.Drawing.Common` → Namespace: `System.Drawing`, NuGet `System.Drawing.Common` nötig falls nicht enthalten
