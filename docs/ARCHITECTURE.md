# ReadyUp — Architecture

ReadyUp is a Windows desktop game launcher (Steam Big Picture–style) built on
.NET 8 and WPF. This document describes the module breakdown, data model,
event flow, and the rationale behind the key technical decisions.

## 1. Goals recap

- Scan local drives + registry for installed games, plus manual add.
- Auto-populate metadata (title, description, publisher, playtime, art).
- Manual metadata editing and game-art replacement (SteamGridDB-Decky style).
- Windowed and full-screen ("Big Picture") modes with smooth transitions.
- Full keyboard, mouse, and gamepad navigation.
- Scalable UI (manual scaling slider, high-refresh-rate friendly).

## 2. Framework choice

**WPF on .NET 8** is the primary target:

- Mature, flexible styling/templating model — ideal for a heavily re-skinned
  "Big Picture" UI (WinUI 3 is more restrictive here and has weaker
  multi-window / borderless full-screen support today).
- `LayoutTransform`/`RenderTransform` give trivial, GPU-composited UI scaling.
- First-class support for custom themes, attached behaviors, and the kind of
  focus-visual overhaul a controller-first UI needs.
- Runs on Windows 10/11 without MSIX packaging friction, simplifying
  side-loaded distribution and drive/registry access.

WinUI 3 is a viable alternative if deeper Fluent/Mica integration or a
Store-first distribution model is desired later; the Core/Data/Scanning/
Metadata/Input libraries below are UI-framework agnostic and would be reused
as-is (only the `ReadyUp.App` project would be rewritten).

## 3. Module breakdown

```
ReadyUp.sln
 ├─ ReadyUp.Core       Domain models, interfaces, orchestration services.
 │                     No Windows-only APIs. Everything else depends on this.
 ├─ ReadyUp.Data        Persistence: SQLite (library DB), JSON (settings),
 │                     file-system art cache.
 ├─ ReadyUp.Scanning    Drive/registry scanning, executable heuristics,
 │                     scan-result caching, manual add.
 ├─ ReadyUp.Metadata    IMetadataProvider / IArtProvider implementations:
 │                     local heuristics, icon extraction, SteamGridDB art,
 │                     IGDB-style metadata (stub), aggregation/merge logic.
 ├─ ReadyUp.Input       XInput P/Invoke wrapper, gamepad polling service,
 │                     gamepad → UI-navigation-command translation.
 └─ ReadyUp.App         WPF shell: windowed + full-screen views, view models,
                        controls, theming, window-mode switching, UI scaling.
```

Dependency direction is strictly one-way:

```
ReadyUp.App
   │
   ├──> ReadyUp.Input
   ├──> ReadyUp.Metadata ──> ReadyUp.Core
   ├──> ReadyUp.Scanning ──> ReadyUp.Core
   └──> ReadyUp.Data ─────> ReadyUp.Core

ReadyUp.Core has no project references (only BCL/NuGet primitives).
```

This keeps scanning/metadata/input testable and reusable independent of the
UI, and lets Core stay a plain data+contracts layer.

## 4. Data model

### 4.1 `Game` (ReadyUp.Core.Models)

| Field              | Type        | Notes                                          |
|--------------------|-------------|-------------------------------------------------|
| Id                 | Guid        | Stable identity, generated on first discovery.  |
| Title              | string      | Display name; user-editable.                    |
| SortingTitle       | string      | Normalized ("The Witcher 3" → "Witcher 3, The"). |
| Description        | string?     | Long-form summary.                              |
| Publisher          | string?     |                                                  |
| Developer          | string?     |                                                  |
| ExecutablePath     | string      | Absolute path to the launch target.             |
| InstallDirectory   | string      | Root folder used for re-scans / art lookup.      |
| Source             | GameSource  | Scanned, Manual, Steam, Epic, GOG, ...           |
| IconPath           | string?     | Local cache path.                                |
| BannerPath         | string?     | 460×215-ish horizontal art.                      |
| BoxArtPath         | string?     | 600×900 portrait art.                            |
| BackgroundPath     | string?     | Full-bleed hero background.                      |
| PlaytimeMinutes    | long        | Accumulated playtime.                            |
| LastPlayedUtc      | DateTime?   |                                                   |
| DateAddedUtc       | DateTime    |                                                   |
| Tags               | List<string>| User/metadata tags (genre, category).            |
| LaunchArguments    | string?     | Optional CLI args passed on launch.              |
| IsHidden           | bool        | Soft-hide from library without deleting.         |
| IsFavorite         | bool        |                                                   |

### 4.2 `ArtAsset`

Represents one replaceable art slot for a game (`ArtAssetType`: Icon, Banner,
BoxArt, Background). Stores `LocalPath`, optional `SourceUrl`, dimensions,
and provenance (`ArtSource`: LocalFile, SteamGridDb, Extracted).

### 4.3 `MetadataRecord`

A provider's *proposed* field values for a game before merge, tagged with a
`Confidence` (0–1) and `ProviderName`, so the aggregator can prefer
higher-confidence/non-empty values and never silently clobber a manual edit
(`Game` tracks a `ManuallyEditedFields` set; the aggregator skips those).

### 4.4 `AppSettings`

UI scale (0.75–1.5), preferred startup mode (Windowed/FullScreen), controller
deadzone, target frame pacing hints, library folders, and API keys for
optional online providers (SteamGridDB, IGDB) — all stored in
`%LOCALAPPDATA%\ReadyUp\settings.json`.

### 4.5 Storage

- **SQLite** (`%LOCALAPPDATA%\ReadyUp\library.db`) for the `games` and
  `art_assets` tables — relational queries (favorites, recently played,
  search) are trivial and the file is a single portable blob users can back
  up. Access via a thin hand-rolled repository (`Microsoft.Data.Sqlite`) to
  avoid pulling in a full ORM for ~2 tables.
- **JSON** for `AppSettings` and the scan cache (`scan-cache.json`) — small,
  human-editable, no migration story needed.
- **File-system art cache** under `%LOCALAPPDATA%\ReadyUp\ArtCache\{gameId}\`
  — keeps the DB free of BLOBs and lets the UI bind `Image.Source` directly
  to a file path.

## 5. Game scanning pipeline

```
                 ┌────────────────────┐
                 │  CompositeGameScanner│  (ReadyUp.Scanning)
                 └─────────┬───────────┘
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                    ▼
┌───────────────┐  ┌──────────────────────┐  ┌─────────────────────┐
│DriveGameScanner│  │RegistryInstalledApps │  │ManualGameAdder       │
│(walks fixed    │  │Scanner (Uninstall    │  │(user-picked exe/dir) │
│drives, known   │  │registry keys)        │  │                      │
│install dirs)   │  └──────────────────────┘  └─────────────────────┘
└───────┬────────┘
        ▼
┌────────────────────┐
│ExecutableHeuristics │  scores candidate .exe files (excludes
│                     │  uninstallers/redistributables/launchers by
│                     │  filename & path patterns, prefers exe co-located
│                     │  with game assets, matches folder name to title)
└─────────┬───────────┘
          ▼
┌────────────────────┐        raises GameDiscovered
│     ScanCache        │◄──── events, deduped by
│ (skip unchanged dirs │      ExecutablePath
│  via mtime/size hash)│
└──────────────────────┘
```

`CompositeGameScanner.ScanAsync()` fans candidate sources out in parallel,
merges/dedupes by normalized executable path, and yields
`ScanResult` items via `IAsyncEnumerable<ScanResult>` so the UI can populate
incrementally instead of blocking on a full scan.

Sources, in order of trust for a first-pass title guess:

1. Registry `Uninstall` keys (`DisplayName`, `InstallLocation`,
   `Publisher`, `DisplayIcon`) — most reliable for “real” installs.
2. Known install directories (`Program Files`, `Program Files (x86)`,
   `SteamLibrary\steamapps\common`, `Epic Games`, `GOG Games`, plus any
   user-added library folders) combined with heuristics.
3. Raw drive walk (opt-in, depth-limited) for anything installed outside
   those roots.

## 6. Metadata pipeline

```
ScanResult ──> MetadataAggregatorService.EnrichAsync(game)
                 │
                 ├─> LocalHeuristicMetadataProvider   (title cleanup, icon
                 │                                     extraction from exe)
                 ├─> SteamGridDbArtProvider            (banner/box-art/hero
                 │                                     lookup, optional —
                 │                                     requires API key)
                 └─> IgdbMetadataProvider              (description,
                                                         publisher/developer,
                                                         optional — requires
                                                         API key)
```

Each provider implements `IMetadataProvider` (text fields) and/or
`IArtProvider` (image fields) and returns a `MetadataRecord`. The aggregator
merges records: non-empty + higher confidence wins, fields the user has
manually edited are never overwritten by automatic enrichment. Providers run
concurrently with a per-provider timeout so a slow/offline API never blocks
the local heuristic pass.

## 7. Game-art replacement

- **Desktop mode**: right-click a tile → context menu → *Change Game Art*
  opens `ChangeArtDialog` (a modal `Window`) with tabs for Icon / Banner /
  Box Art / Background. Each tab offers *Browse local file* and
  *Search SteamGridDB* (grid of thumbnails, click to apply).
- **Full-screen mode**: holding **Y** raises `GameOptionsRequested` from
  `GamepadNavigationManager`; the full-screen shell shows a controller-first
  `GameOptionsOverlay` (radial/list menu) with the same *Change Game Art*
  entry, reusing `ChangeArtViewModel` but rendered with a gamepad-navigable
  grid instead of dialog controls.
- Applying a selection copies/downloads the image into the art cache,
  updates the `ArtAsset` row, and raises `ArtChanged` so any bound
  `GameTileControl`/details view refreshes immediately (the `Image` control
  is bound to a `BitmapImage` wrapped with `CacheOption.OnLoad` so the file
  can be replaced on disk without a stale handle).

## 8. Controller support

`ReadyUp.Input` wraps raw `XInput1_4.dll` via P/Invoke (no third-party
gamepad dependency — XInput covers Xbox-compatible pads, which is the
overwhelming majority of PC controllers including Bluetooth/DS4 through
DS4Windows-style XInput emulation).

- `GamepadService` polls up to 4 controller slots on a background timer
  (~8 ms tick, matched to typical 120 Hz+ vsync so the UI never perceives
  input lag), debounces button edges, and applies a configurable deadzone
  to the thumbsticks.
- `GamepadNavigationManager` turns raw state into semantic
  `NavigationCommand`s (`Up/Down/Left/Right/Accept/Back/OpenOptions/Menu`)
  with stick-repeat (initial delay + repeat rate, like a keyboard's key
  repeat) so holding a direction scrolls the grid smoothly.
- The WPF side consumes `NavigationCommand`s through a `GamepadFocusBehavior`
  attached behavior that moves `Keyboard.Focus`/`FocusManager` logical focus
  in the same directional-navigation graph WPF already uses for arrow keys —
  so gamepad, keyboard, and mouse all converge on one focus model and every
  view only needs to handle `Accept`/`Back`/`OpenOptions` explicitly.

## 9. Windowed ⇄ full-screen switching

`WindowModeManager` owns a single `FullScreenWindow` and a single
`MainWindow` sharing the same `LibraryViewModel`/`SelectedGameViewModel`.
Switching mode:

1. Animates a fade (150 ms `DoubleAnimation` on a `VisualBrush` snapshot of
   the outgoing window) to avoid a visible flash/resize pop.
2. Toggles `WindowStyle`/`WindowState`/`ResizeMode` on the target window
   (`WindowStyle.None` + `WindowState.Maximized` + topmost per-monitor
   bounds for full-screen, so it correctly spans the monitor the app is
   currently on rather than assuming primary).
3. Hides the previous window instead of closing it, so navigation
   state/animations aren't rebuilt on every toggle.

Triggered by **Alt+Enter**, a settings toggle, or **Start** button on the
gamepad (full-screen → windowed only, since Big Picture is meant to stay
full-screen otherwise).

## 10. UI scaling

`UiScaleService` exposes a `double Scale` (0.75–1.5, persisted in
`AppSettings`) applied via a `LayoutTransform` (not `RenderTransform`, so
layout re-measures at the new size instead of just stretching pixels) on the
root `Grid` of each shell window. A settings slider updates it live. Because
it's a layout-level transform, it composes correctly with per-monitor DPI
scaling that WPF already applies, and stays crisp at 120/144/165 Hz since
there's no per-frame recompute involved — it's a one-time transform until
the user moves the slider again.

## 11. Event flow summary

```
User launches app
   → AppHost builds DI container (Core/Data/Scanning/Metadata/Input/App)
   → LibraryViewModel.InitializeAsync()
        → loads cached games from SqliteGameRepository (instant UI paint)
        → kicks off CompositeGameScanner.ScanAsync() in the background
             → GameDiscovered → GameLibraryService.UpsertAsync()
                  → SqliteGameRepository.Save()
                  → LibraryViewModel adds/updates GameViewModel (Dispatcher)
                  → MetadataAggregatorService.EnrichAsync() fire-and-forget
                       → provider results merged → repository updated
                       → GameViewModel refreshed again when art/text lands
   → GamepadService starts polling
        → NavigationCommand events → GamepadFocusBehavior / active ViewModel
   → User selects a game → Launch (Process.Start) → playtime tracking starts
        on process exit → PlaytimeMinutes persisted
```

## 12. Performance & Windows-app best practices

- Scanning and metadata enrichment run entirely off the UI thread; only the
  final `ObservableCollection` mutation is marshalled to the dispatcher.
- Image assets are decoded at their display size (`DecodePixelWidth`) to
  avoid full-resolution decodes for small tiles, and cached as `BitmapImage`
  with `BitmapCacheOption.OnLoad` so file handles are released immediately.
- `VirtualizingStackPanel`/`ItemsControl` virtualization is enabled on the
  game grid so libraries with hundreds of titles stay smooth.
- Composition/animation uses `RenderOptions.BitmapScalingMode=HighQuality`
  only on static art; tile-hover animations use lightweight opacity/scale
  transforms to stay cheap on integrated GPUs at high refresh rates.
- SQLite writes are batched/async; the UI never blocks on disk I/O.
- Gamepad polling runs on its own high-priority background timer, isolated
  from layout/render passes.

## 13. Future expansion

- Cloud save sync (per-game save-folder mapping + a pluggable backend:
  OneDrive/Drive/self-hosted).
- Achievements aggregation (read Steam/Xbox APIs where available, fall back
  to local heuristic achievement files).
- Deeper playtime analytics (sessions, heatmap calendar).
- Additional launcher-import providers (Steam/Epic/GOG/Battle.net library
  import instead of relying purely on filesystem+registry heuristics).
- Themes / community skins for the full-screen shell.
- Controller vibration feedback on navigation (XInput `SetState`).
