# Idle Tic Tac Toe — Work Samples

A short excerpt from **Idle Tic Tac Toe**, a Unity idle game whose boards play tic-tac-toe to earn Gum, then spend it on upgrades, prestige, and a skill tree.

These are exact copies of live source, meant to be **read**, not compiled. They show how new content is added without rewriting the core loop.

Start with `Board Simulation` (the gameplay object), then `Bonus Aggregation` and `Skill Tree` (how modifiers and meta upgrades plug in), then `Big Number Arithmetic` (the number type everything else sits on).

---

## Board Simulation

`Board` is an N×N tic-tac-toe match that ticks itself: tiles, turn timing, win/loss/draw, smart vs dumb plays (tile utility), golden tiles, and Play Quicker bursts. When a tier board is fast enough, it switches to **passive analytic mode** (expected wins/second) instead of animating every move.

`ManualBoard` subclasses that loop for the player-tappable board — pot (other boards bank Gum into it) and win streak — without copying the sim. `BoardUpgrade` is the same idea for purchases: shared buy-1 / buy-5 / buy-max cost math, then a small `Apply()` per stat. `WinValueUpgrade` and `PlaySpeedUpgrade` are two of those.

- `Board.cs`, `Tile.cs`, `ManualBoard.cs` — `Assets/Scripts/Gameplay/`
- `BoardUpgrade.cs`, `WinValueUpgrade.cs`, `PlaySpeedUpgrade.cs` — `Assets/Scripts/Gameplay/Board Upgrades/`

---

## Bonus Aggregation

`BonusManager` is the modifier bus. Effects are keyed by **type** (`BonusType`) and **source** (prestige, a skill tier, a timed buff, …) and stored as `Big`, `float`, or `bool`. Totals are cached; listeners hear about changes.

A new multiplier is an enum member plus `UpdateBonus*` from whatever system owns it. Boards and UI never need to know who granted the bonus.

- `BonusManager.cs` — `Assets/Scripts/Managers/`

---

## Skill Tree

Skills are ScriptableObjects: id, cost, dependencies, `Apply()`. The tree is a list of those assets. Adding a skill is a new subclass that writes into `BonusManager`; the UI and save layer keep working.

The three examples map to the three bonus value types:

| Skill | Bonus |
|---|---|
| `BasicGumMultiplierSkill` | `Big` gum earnings |
| `TrickleDownGumenomicsSkill` | `float` share of higher-board rewards |
| `PlayFasterAllAtOnceSkill` | `bool` global Play Faster |

`SkillOrderValidator` is an editor menu that checks unique, gap-free `Order` values across all skill assets.

- `Skill.cs`, `BasicGumMultiplierSkill.cs`, `TrickleDownGumenomicsSkill.cs`, `PlayFasterAllAtOnceSkill.cs` — `Assets/Scripts/Gameplay/Skill Tree/Skills/`
- `SkillTree.cs` — `Assets/Scripts/Gameplay/Skill Tree/`
- `SkillOrderValidator.cs` — `Assets/Editor/Scripts/`

---

## Big Number Arithmetic

Idle currencies outgrow `double`. `Big` is `coefficient × 10^exponent`, with normalize, arithmetic, `Pow`/`Log`, and player-facing shorthand (`aa`, `ab`, …) or scientific notation.

The Inspector drawer edits coefficient and exponent in place. `BigNumberTests` is an in-engine test module (not NUnit) covering equality, normalize, powers, and comparisons.

- `Big.cs` — `Assets/Scripts/Util/`
- `BigNumberPropertyDrawer.cs` — `Assets/Editor/Scripts/`
- `BigNumberTests.cs`, `TestModule.cs` — `Assets/Unit Test/Scripts/`
