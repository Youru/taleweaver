# Taleweaver

[![CI](https://github.com/Youru/taleweaver/actions/workflows/ci.yml/badge.svg)](https://github.com/Youru/taleweaver/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/Youru/taleweaver?include_prereleases)](https://github.com/Youru/taleweaver/releases)

**A game master's companion for dungeon-crawl board games.** Taleweaver runs the scenario, plays the enemies and keeps every count, so the person at the head of the table can focus on the story. Players join from their phones.

Physical dice, physical board: the app never replaces the table, it takes the bookkeeping off it.

> Unofficial fan project, made for the board game *Middara: Unintentional Malum*. It is not affiliated with or endorsed by Succubus Publishing. **This repository contains no content from the game**: it ships with a small demonstration pack, and you can write your own.

## Features

- **Scenarios in steps**: narrative scenes, encounters, choices, and events that fire on a round, a token or a defeated enemy.
- **Enemy AI**: the app asks the GM yes/no questions ("Is an opponent adjacent?") and gives the resulting instructions. It also runs the initiative track, health, effects and the round.
- **The table**: a map of the encounter, with setup, fog by zone, and optional movement tracking. From positions, terrain and line of sight, it suggests AI answers, targets and moves. The GM stays the referee.
- **Phones and tablets (the room)**: players scan a QR code to see their sheet, their cards and the public map. The GM can also move to a tablet the same way. Secret information never reaches a phone.
- **Campaign tracking**: party, equipment, disciplines, rewards and saves you can export.
- **Workshop**: a visual editor for scenarios and maps, a checker that catches mistakes, and print-ready encounter sheets.
- **Rules at hand**: searchable rules reference, in French and English.
- **One file to run**: a single executable for Windows or Linux. Nothing to install, and it opens in your browser.

## Getting started

### Download

Grab the latest build from the [Releases](https://github.com/Youru/taleweaver/releases) page:

- **Windows**: `Taleweaver.exe`. Double-click it. The file is not signed, so Windows may show "Windows protected your PC": choose *More info*, then *Run anyway*.
- **Linux**: `Taleweaver-linux-x64.tar.gz`. Run `tar -xzf Taleweaver-linux-x64.tar.gz`, then `./Taleweaver`.

Your browser opens on the app. Keep the console window open while you play, and close it to quit. Release builds contain the demonstration content only.

### Adding game content

The game's content comes as a separate **content pack**, a `.taleweaver.zip` file that is never published here. To add one, either:

- on the home page, under **Game content**, choose **Add a content pack** and pick the file; or
- put the file beside `Taleweaver.exe` (or the Linux `Taleweaver`) before starting the app: it is installed at start, and again whenever the file changes.

Updating the app does not touch installed packs. Remove a pack from the same section of the home page.

### Phones and tablets

The phones must be on the same Wi-Fi as the computer running Taleweaver. Allow the firewall prompt for private networks. In a game, the **Room** button shows the QR code for players, and **Screens** shows one for a GM tablet or a shared table display. Use a trusted network: traffic is not encrypted.

## Build from source

Requirements: [.NET SDK 10](https://dotnet.microsoft.com/download) and [Node.js 22](https://nodejs.org/).

```bash
git clone https://github.com/Youru/taleweaver.git
cd taleweaver

# API on http://localhost:5145, on the demonstration content
Content__RootPath=../../demo dotnet run --project src/Taleweaver.Api

# In a second terminal: the web app on http://localhost:5173
cd web
npm ci
npm run dev
```

On Windows PowerShell, set the variable with `$env:Content__RootPath = "../../demo"` before `dotnet run`.

Run the checks the CI runs:

```bash
dotnet test                 # API: unit and integration tests
cd web
npm run typecheck
npm run lint
npm test                    # web: unit tests (Vitest)
```

Package the single-file app (needs Python 3 and Pillow):

```bash
python3 -I scripts/package.py --content demo          # both systems, or add win-x64 / linux-x64
```

## Content

Taleweaver reads its content from a folder with three parts:

```
packs/<pack>/       catalogue: combatants, items, tiles, tokens, dice, initiative cards…
scenarios/<pack>/   scenarios, one JSON file each
assets/             pictures named by the catalogue
```

[`demo/`](demo/) is a complete, tiny example: one pack, one scenario. A content pack is the same three folders (plus `reference/rules/rulebook/` for the rules) zipped with a `content-pack.json` at the root (`{ "id": "my-pack", "name": { "en": "My pack" }, "version": "1" }`); `python3 -I scripts/package.py --content <folder> --content-pack <id>` makes one, pictures converted to WebP. Set `Content__RootPath` (or `TALEWEAVER_CONTENT_ROOT` for the tests and the packaging script) to use another folder. Scenarios made in the workshop go to `scenarios/homebrew/` of that folder; the packaged app keeps them, and the games, in your user folder.

## Project layout

| Path | What |
|---|---|
| `src/Taleweaver.Api/` | ASP.NET Core API: content loading, the game engine (pure, replayable), rooms, printing |
| `web/` | Vue 3 + PrimeVue front end: GM screens, the table, the workshop, the phone views |
| `tests/` | xUnit unit and integration tests. Playwright end-to-end tests live in `web/e2e/` |
| `demo/` | Demonstration content |
| `scripts/package.py` | Builds the single-file executables |

## Status

Taleweaver is a personal project in active development, used at the author's table. Expect the scenario format to change between versions. Developer notes (in French): [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## License

No license has been chosen yet: until one is added, all rights are reserved. The game *Middara*, its names, texts and pictures belong to Succubus Publishing.
