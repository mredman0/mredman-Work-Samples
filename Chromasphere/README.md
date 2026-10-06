# ChromaSphere — Work Samples

A short excerpt from **ChromaSphere: 3D Line Puzzle**, a shipped Unity mobile game (Android and iOS). Players connect matching colored nodes on a sphere without crossing paths, spend hints and unlock daily packs.

These are exact copies of live source, meant to be **read**, not compiled. They still call into auth, localization, Unity IAP, LevelPlay, and Cloud Save, which live outside this folder.

Start with `Puzzle Gameplay` (the spherical match), then `Daily Puzzles` (how new content is generated and fetched), then `In App Purchases` and `Ads and Hints` (how store and ads plug into hints and dailies).

---

## Puzzle Gameplay

`Puzzle` is an N-row spherical match that runs itself: grid, path drawing, collisions, undo/reset, hints, connecting node pairs, taking warps, and `PuzzleCompleted` when every pair is solved. A drag threshold decides input vs camera pan so orbiting the sphere does not start a line.

`PuzzleConfig` is the ScriptableObject for one puzzle — grid shape, node/waypoint/warp/wall positions, baked solutions, starting camera. `PuzzleGrid` and `GridCell` turn `cellsPerRow` into latitude/longitude cells with neighbor topology and great-circle edges. `PolarVector3` is the lat/long/radius type used instead of thinking in XYZ.

The rest is constraints and presentation. `Node`, `Waypoint`, `Warp`, and `Wall` sit on the grid. `CameraController` is the orbit arm: drag, momentum, pitch limits, aspect-ratio fit, post-solve spin. `MultiLineRenderer` draws a path as segments so a route can wrap the sphere and jump through a warp. `PuzzleProvider` is the scene-to-scene handoff of the config being loaded. `ColorScheme` / `ColorManager` swap player palettes, including which schemes are unlocked.

- `Puzzle.cs`, `PuzzleConfig.cs`, `PuzzleGrid.cs`, `GridCell.cs`, `PolarVector3.cs`
- `Node.cs`, `Waypoint.cs`, `Warp.cs`, `Wall.cs`
- `CameraController.cs`, `MultiLineRenderer.cs`
- `PuzzleProvider.cs`, `PackInfo.cs`, `ColorScheme.cs`, `ColorManager.cs`

---

## Daily Puzzles

Two halves of the same live-ops loop: the mobile client that fetches today's puzzles, and a headless generator that creates them from a date seed. Store receipt validation on the server is not in this sample.

The client caches today's JSON and hydrates `PuzzleConfig` objects. `DailyPuzzleManager` sends store type plus purchase tokens so the server can unlock paid difficulty packs, and refetches when IAP entitlements change. `DailyPuzzlesPage` waits until puzzles are ready, then builds a section per pack (and an unlock-more row when paid packs are locked). `DailyPuzzleStreakDisplay` is how many dailies were finished today vs the streak threshold.

The generator is a “lite” copy of the in-editor builder: no GameObjects, seeded randomness, difficulty curves. `DailyPuzzleGenManager` keeps yesterday/today/tomorrow in memory and filters packs by availability keys. `PuzzlePackGeneratorLite` samples a `PackGenerationConfig` (node pairs, warps, walling) and retries failed builds; `PuzzleBuilderLite` places walls, pairs, waypoints, and warps, then searches for a valid path. JSON-friendly stand-ins (`MyAnimationCurve`, `MyQuaternion`, `PolarVector3`) keep Unity types out of the server runtime.

- `DailyPuzzleManager.cs`, `DailyPuzzlesPage.cs`, `DailyPuzzlesSection.cs`, `DailyPuzzlesPageButton.cs`, `DailyPuzzleStreakDisplay.cs` — `Client/`
- `DailyPuzzleGenManager.cs`, `Random.cs` — `Generator/`
- `PuzzlePackGeneratorLite.cs`, `PuzzleBuilderLite.cs`, `PuzzleGridLite.cs`, `GridCellLite.cs`, `PuzzleObject*.cs` — `Generator/PuzzleBuilderLite/`
- `PackGenerationConfig.cs`, `PuzzleConfig.cs`, `PuzzleConfigPayload.cs`, `PuzzlesPayload.cs` — `Generator/Data/`
- `MyAnimationCurve.cs`, `MyQuaternion.cs`, `PolarVector3.cs` — `Generator/Data/`

---

## In App Purchases

`PurchaseManager` is the Unity IAP v5 catalog: ad-free, hint packs, and daily-puzzle difficulty unlocks. It registers products, checks entitlements on startup, confirms pending orders, grants hint payouts, tracks owned non-consumables, and restores on iOS/Android (with an editor fake path). `AdFreeChanged` and `DailyPuzzleAccessChanged` let ads and dailies react without knowing store details.

`PurchaseItem` is one store row — localized price, owned state, buy. `PurchaseUIController` is the overlay: lock puzzle/camera input while open, failed-purchase dialog, hide ad-free UI when ads are compiled out. Google Play license-key obfuscation and store receipt validators are not in this sample.

- `PurchaseManager.cs`, `PurchaseItem.cs`, `PurchaseUIController.cs`

---

## Ads and Hints

`AdManager` initializes LevelPlay (banner, interstitial, rewarded), caps interstitials by puzzles completed, and respects ad-free. App keys and ad unit IDs are inspector fields, not hardcoded. `HintManager` is the hint wallet: Cloud Save when authenticated, local storage offline (including hints spent offline), and IAP hint payouts.

`AdForHint` is the rewarded button — enable when a placement is loaded, grant hints on reward, hide when ads are off. `AdAwarePanel` shifts UI above the banner and collapses that offset when ad-free is owned. `HintCounter` is the HUD (`∞` in demo builds).

- `AdManager.cs`, `HintManager.cs`, `AdForHint.cs`, `AdAwarePanel.cs`, `HintCounter.cs`
