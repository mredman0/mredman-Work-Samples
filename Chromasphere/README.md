# ChromaSphere

Work samples from **ChromaSphere: 3D Line Puzzle**, a shipped mobile game (Android and iOS) by Red Prism Games. Players connect matching colored nodes on a sphere without crossing paths, with waypoints, walls, and warps as extra constraints.

These files are **exact copies** of production scripts. They are not a buildable Unity project: they still reference other game systems (auth, localization, addressables, Unity IAP, LevelPlay, Cloud Save) that live outside this folder.

Internal project name: `Global-Link`. Product name: ChromaSphere.

---

## Puzzle-Gameplay

Core 3D puzzle: a latitude/longitude grid on a sphere, path drawing, win detection, and camera orbit.

| File | What it does |
|---|---|
| `Puzzle.cs` | Main gameplay controller. Handles input vs camera pan, drawing a path cell-to-cell, collisions, undo/reset, hints, connecting node pairs, taking warps, and firing `PuzzleCompleted` when every pair is solved. |
| `PuzzleConfig.cs` | ScriptableObject that describes one puzzle: grid shape, node/waypoint/warp/wall positions, colors, baked solutions, and starting camera view. |
| `PuzzleGrid.cs` | Builds the spherical grid from `cellsPerRow`, wires neighbor relationships, and draws great-circle connections between cells. |
| `GridCell.cs` | One cell on the sphere. Converts row/column into latitude/longitude bounds, places itself in Cartesian space, and exposes neighbor / dead-end queries used by generation and pathing. |
| `PolarVector3.cs` | Latitude/longitude/radius helper used everywhere the game thinks in spherical coordinates instead of XYZ. |
| `Node.cs` | A colored endpoint the player must pair with its match. |
| `Waypoint.cs` | A cell the matching-color path is required to pass through. |
| `Warp.cs` | A paired portal. A path that enters one warp continues from its pair, including the preview line through the sphere. |
| `Wall.cs` | An impassable cell. |
| `CameraController.cs` | Orbit camera on an arm around the sphere: drag, momentum, pitch limits, aspect-ratio fit, and the post-solve spin. |
| `MultiLineRenderer.cs` | Renders a path as multiple line segments so a route can wrap the sphere and jump through warps. |
| `PuzzleProvider.cs` | Tiny scene-to-scene handoff that carries the `PuzzleConfig` being loaded. |
| `PackInfo.cs` | Display metadata for a puzzle pack (name, tint) used by menus. |
| `ColorScheme.cs` / `ColorManager.cs` | Loads and switches player-selected palettes, including which schemes are unlocked. |

What this sample shows: custom spherical geometry, grid topology, and a fairly large gameplay state machine (input, drawing, collisions, undo, completion) rather than a tutorial-scale puzzle.

---

## Daily-Puzzles

Two halves of the daily-puzzle feature: the mobile client that fetches and presents today's puzzles, and the headless generator that creates them.

Store receipt validation on the server is **not** included here.

### Client

| File | What it does |
|---|---|
| `DailyPuzzleManager.cs` | Fetches today's puzzle JSON from the daily-puzzle HTTP API, caches it on disk, and hydrates `PuzzleConfig` objects. Sends store type plus purchase tokens so the server can unlock paid difficulty packs. Refetches when IAP entitlements change. |
| `DailyPuzzlesPage.cs` | Menu page that waits for puzzles to be ready, then builds a section per difficulty pack and shows an “unlock more” entry when paid packs are locked. Editor/marketing builds also get a date picker for on-demand fetches. |
| `DailyPuzzlesSection.cs` | One difficulty row: localized title, pack tint, and a button per puzzle id. |
| `DailyPuzzlesPageButton.cs` | Main-menu entry that routes into the daily-puzzles page. |
| `DailyPuzzleStreakDisplay.cs` | Streak UI: how many dailies were finished today vs the threshold to keep the streak, and the current streak count. |

### Generator

Headless “lite” copies of the in-editor puzzle builder. No Unity GameObjects; they generate `PuzzleConfig` data from seeded randomness and difficulty curves.

| File | What it does |
|---|---|
| `DailyPuzzleGenManager.cs` | Loads pack-generation configs, keeps yesterday/today/tomorrow in memory, regenerates when the calendar day rolls over, and filters which packs a requester may receive based on availability keys. |
| `PuzzleBuilderLite/PuzzlePackGeneratorLite.cs` | Turns a `PackGenerationConfig` plus a master seed into a list of puzzles. Samples difficulty curves (node pairs, warps, walling) per puzzle index and retries failed builds. |
| `PuzzleBuilderLite/PuzzleBuilderLite.cs` | Procedural builder: place walls, node pairs, waypoints, and warps on the lite grid, then search for a valid solution path. |
| `PuzzleBuilderLite/PuzzleGridLite.cs` / `GridCellLite.cs` | Grid topology used during generation (neighbors, neighborhoods) without rendering. |
| `PuzzleBuilderLite/PuzzleObject*.cs` | Lite stand-ins for nodes, waypoints, warps, and walls. |
| `Data/PackGenerationConfig.cs` | Difficulty recipe for a pack: grid shape, min/max node and warp pairs, and animation curves for walling/clustering over the pack. |
| `Data/PuzzleConfig.cs` / `PuzzleConfigPayload.cs` / `PuzzlesPayload.cs` | Serializable puzzle records and the JSON payload the client downloads. |
| `Data/MyAnimationCurve.cs` / `MyQuaternion.cs` / `PolarVector3.cs` | JSON-friendly stand-ins for Unity types the generator needs without a full editor runtime. |
| `Random.cs` | Seeded RNG used so a date + pack config always regenerates the same puzzles. |

What this sample shows: a live-ops loop (generate on a server, fetch on device, gate by IAP) and a seeded content pipeline instead of hand-authoring every daily.

---

## In-App-Purchases

Unity IAP v5 flow for a small catalog: ad-free, hint packs, and daily-puzzle difficulty unlocks.

| File | What it does |
|---|---|
| `PurchaseManager.cs` | Connects `StoreController`, registers product definitions, fetches purchases, checks entitlements on startup, confirms pending orders, grants hint payouts, tracks owned non-consumables, and restores transactions on iOS/Android (with an editor fake path). Fires `AdFreeChanged` and `DailyPuzzleAccessChanged` so other systems can react. |
| `PurchaseItem.cs` | One store-row widget: localized price, owned state for non-consumables, and “buy” which calls `InitiatePurchase`. Hides the ad-free row when ads are compiled out. |
| `PurchaseUIController.cs` | Store overlay: locks puzzle/camera input while open, shows a dialog on failed purchases, and hides ad-free UI when ads are disabled. |

Google Play license-key obfuscation (`GooglePlayTangle`) and App Store / Play receipt validators are **not** included.

What this sample shows: wiring a real store catalog into gameplay systems (hints, ads, dailies) including restore and startup entitlement checks, not just a purchase button.

---

## Ads-and-Hints

LevelPlay ads plus a hint currency that can be earned from rewarded ads, bought with IAP, or used offline with later sync.

| File | What it does |
|---|---|
| `AdManager.cs` | Initializes LevelPlay (banner, interstitial, rewarded). Caps interstitials by puzzles-completed, respects an ad-free entitlement, and exposes load/show/fail events. App keys and ad unit IDs are inspector fields (not hardcoded in this file). |
| `HintManager.cs` | Hint wallet. Syncs count through Unity Cloud Save when authenticated; falls back to local storage offline, including hints spent while offline. Listens to IAP hint payouts. |
| `AdForHint.cs` | Rewarded-ad button. Enables when a rewarded placement is loaded; on reward, grants hints and queues the next ad. Hidden when ads are disabled. |
| `AdAwarePanel.cs` | Shifts UI safe area to sit above the banner, and collapses that offset when ad-free is owned. |
| `HintCounter.cs` | HUD for remaining hints (`∞` in demo builds). |

What this sample shows: ads as a resource loop (watch → hint) integrated with IAP, cloud save, and layout, rather than a fire-and-forget interstitial.

---

## Not included on purpose

- Store signing keys, Apple `.p8`, Google Play service-account JSON
- Unity IAP Google Play tangle / license key
- App Store / Play purchase-validation server
- Adjust analytics tokens
- LevelPlay app keys and ad unit IDs (those live on the Ad Manager prefab, not in `AdManager.cs`)
- Marketing videos, fonts, and third-party SDK trees
