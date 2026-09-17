# ReadyUp

A modern, Steam Big Picture–style video game launcher for Windows, built on
.NET 8 and WPF. ReadyUp scans your drives and the registry for installed
games (plus manual add), fetches and lets you edit metadata and box
art/banners/icons/backgrounds, and is fully navigable with keyboard, mouse,
or a gamepad in both windowed and full-screen modes.

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the full module
breakdown, data model, and event flow.

## Features

- **Game discovery** — scans known install roots (Steam/Epic/GOG/Battle.net/
  Program Files/custom folders), the Windows Uninstall registry, and
  optionally a depth-limited raw drive walk; plus manual add by exe or folder.
- **Metadata** — automatic title cleanup, icon extraction, and optional
  online enrichment (IGDB descriptions/publisher/developer, SteamGridDB art),
  all merged without ever overwriting a field you've manually edited.
- **Game-art replacement** — right-click a tile (desktop) or hold **Y**
  (gamepad, full-screen) to browse a local image or search SteamGridDB for
  icons, banners, box art, and backgrounds — the same flow as the
  SteamGridDB Decky plugin.
- **Windowed + full-screen ("Big Picture")** modes with a smooth transition,
  toggled via Alt+Enter, a toolbar button, or Start (full-screen → windowed).
- **Full controller support** — D-pad/stick navigation with key-repeat, A to
  select, B to back out, Y for game options, Start for the main menu — all
  routed through WPF's native focus system so keyboard, mouse, and gamepad
  stay in sync.
- **Manual UI scaling** (0.75×–1.5×) via a settings slider, applied through a
  `LayoutTransform` so it composes correctly with per-monitor DPI and stays
  crisp at 120 Hz+ refresh rates.

## Solution layout

```
ReadyUp.sln
Directory.Build.props        Shared TargetFramework/Nullable/LangVersion for every project
docs/
  ARCHITECTURE.md            Full architecture writeup
src/
  ReadyUp.Core/               Domain models + service interfaces + orchestration (no Windows-only APIs)
  ReadyUp.Data/                SQLite library DB, JSON settings/scan-cache, art cache
  ReadyUp.Scanning/            Drive scanner, registry scanner, executable heuristics, manual add
  ReadyUp.Metadata/            Local heuristics, icon extraction, SteamGridDB + IGDB providers, aggregator
  ReadyUp.Input/                XInput wrapper, gamepad polling, gamepad → navigation-command mapping
  ReadyUp.App/                 WPF shell: windows, view models, controls, theming, DI composition root
```

Dependencies flow one way: `ReadyUp.App` → `{Input, Metadata, Scanning, Data}`
→ `ReadyUp.Core`. Scanning/Metadata/Input have no dependency on the UI, so
they're reusable if the shell is ever ported to WinUI 3.

## Requirements

- Windows 10/11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 17.9+ (or `dotnet` CLI) with the **.NET Desktop
  Development** workload for WPF designer support

## Building & running

```powershell
dotnet restore ReadyUp.sln
dotnet build ReadyUp.sln -c Debug
dotnet run --project src\ReadyUp.App\ReadyUp.App.csproj
```

Or open `ReadyUp.sln` in Visual Studio and press F5 (`ReadyUp.App` is the
startup project).

On first run ReadyUp creates `%LOCALAPPDATA%\ReadyUp\` for its library
database (`library.db`), settings (`settings.json`), scan cache
(`scan-cache.json`), and art cache (`ArtCache\`).

### Optional online providers

Both are optional — the app is fully usable offline via local heuristics
(title cleanup + icon extraction). Configure keys in **Settings**:

- **SteamGridDB** — art search (banners/box art/backgrounds/icons). Get a
  free API key at steamgriddb.com (account → Preferences → API).
- **IGDB** — descriptions/publisher/developer. Requires a Twitch developer
  application (id.twitch.tv/oauth2 client credentials) — IGDB authenticates
  through Twitch.

## Key dependencies

| Project | Package | Purpose |
|---|---|---|
| ReadyUp.Data | `Microsoft.Data.Sqlite` | Library database |
| ReadyUp.Scanning | `Microsoft.Win32.Registry` | Uninstall-key scanning |
| ReadyUp.Metadata | `System.Drawing.Common` | Executable icon extraction |
| ReadyUp.App | `CommunityToolkit.Mvvm` | MVVM (ObservableObject/RelayCommand source generators) |
| ReadyUp.App | `Microsoft.Extensions.DependencyInjection` | Composition root |
| ReadyUp.App | `Microsoft.Extensions.Http` | `IHttpClientFactory` for the art/metadata providers |

## Controller reference

| Input | Action |
|---|---|
| D-Pad / Left Stick | Navigate |
| A | Select / Launch |
| B | Back / Close overlay |
| Y | Open game options (Change Game Art, Favorite, Remove) |
| Start | Open main menu |
| Alt+Enter (keyboard) | Toggle windowed ⇄ full-screen |

## Roadmap

Cloud save sync, achievements aggregation, deeper playtime analytics,
native Steam/Epic/GOG library import, community themes, and controller
vibration feedback — see [`docs/ARCHITECTURE.md § 13`](docs/ARCHITECTURE.md#13-future-expansion).
