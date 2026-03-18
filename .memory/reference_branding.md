---
name: Branding Guidelines
description: Offizielle Branding-Ressourcen für Discord und FFXIV — relevant für Icons, Logos, UI-Assets
type: reference
---

## Discord Branding

- **Branding Guidelines:** https://discord.com/branding
  - Logos, Farben, Do's & Don'ts für Discord-Symbol-Nutzung
  - Offizielle Logo-Downloads (SVG/PNG: Symbol, Wordmark, Lockup)
  - Primärfarbe: Blurple `#5865F2`

- **Discord Social SDK / Developer Branding:** https://docs.discord.com/developers/discord-social-sdk/design-guidelines/branding-guidelines#discord-logo
  - Spezifische Richtlinien für Entwickler-Integrationen

## FFXIV Branding / Fan Kit

- **FFXIV Fankit (Lodestone):** https://de.finalfantasyxiv.com/lodestone/special/fankit/
  - Offizielle Assets für Fan-Projekte (Logos, Icons, UI-Elemente)

## Plugin Icon

- Datei: `images/icon.svg` / `images/icon.png` (512×512)
- Versionsdateien: `images/icon_v1.svg`, `images/icon_v2.svg`
- Konvertierung SVG→PNG: Node.js + `sharp` (kein globales Install nötig)
  ```bash
  cd images
  npm install --no-save sharp
  node -e "require('sharp')(require('fs').readFileSync('icon.svg')).resize(512,512).png().toFile('icon.png', console.log)"
  # danach node_modules, package.json, package-lock.json löschen
  ```
- `IconUrl` in `repo.json`: `https://raw.githubusercontent.com/havyrl/FFXIVDiscordChatBridge/main/images/icon.png`
