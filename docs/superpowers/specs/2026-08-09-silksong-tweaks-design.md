# Silksong Tweaks — Design

- **Date:** 2026-08-09
- **Status:** Approved, pending implementation plan
- **Repo:** `C:\desktopContents\projects\SilksongTweaks`
- **Licence:** MIT

## Problem

Three separate Silksong mods deliver four settings we actually want, and each does it
differently: one exposes an F8 menu, one is config-file only, one is silent. Worse, none of
them can tell you whether it is *working*.

Observed directly on 2026-08-08: `ReBack` logged `Harmony patch applied successfully`, the
player then died three times, and the mod emitted nothing. The rosaries mod logged on every
one of those deaths. A working mod and a possibly-broken mod were indistinguishable without
reading a log file.

That is the problem this project solves, as much as the consolidation is.

## Scope

**In — four behaviours, reimplemented and owned:**

| Behaviour | Setting |
|---|---|
| Return to death location after respawn | on/off, countdown seconds, cancel key |
| Cocoon never permanently lost | on/off, keep fraction, cocoon stays in place |
| Max health | 1–20 masks |
| Damage taken | multiplier |

**Out — explicitly not built:** per-enemy and per-boss editors, needle upgrade scaling, shop
prices, rewards, presets, favourites, difficulty scoring, sprite galleries. These exist in
CustomDifficulty and are not used.

**Replaces:** `ReBack`, `rosaries never permanently lost`, `CustomDifficulty`. Those three
must be removed before this mod is used — all four behaviours patch the same code paths and
double-patching death is the most likely failure mode for users.

## Decisions

| Decision | Choice | Why |
|---|---|---|
| Scope | Four behaviours only | Small enough to build well and test properly |
| Audience | Engineered to publish, ship when proven | Resilience now, packaging later |
| UI | Styled IMGUI (`OnGUI`) | No art assets, cannot break the game's own UI |
| Input | Keyboard + mouse | Player uses K&M; no gamepad navigation needed |
| Health persistence | Runtime-only, never written to save | Uninstall leaves the save vanilla |
| Licence | MIT | Most permissive, the norm in BepInEx modding |
| Attribution | README + in-game Credits panel | Independent implementation, credit as courtesy |

## Architecture

One assembly, `SilksongTweaks.dll`.

```
Plugin.cs              BepInEx entry. Discovers modules, owns lifecycle.
ITweakModule.cs        Id · DisplayName · Settings · TryApply() · Status · LastFired
ModuleRegistry.cs      Discovery, patch orchestration, status collection
HookTargets.cs         SINGLE source of truth for every patch target
Config/                BepInEx ConfigFile bindings (typed, auto-persisted)
Rules/
  DamageRules.cs       Pure functions, no Unity types
  HealthRules.cs
  CocoonRules.cs
Ui/
  TweakWindow.cs       OnGUI shell: window, sections, hotkey
  Widgets.cs           Styled toggle / slider / label primitives
  Theme.cs             GUIStyles, colours, spacing
  CreditsPanel.cs      Attribution to prior-art authors
Modules/
  ReturnToDeathModule.cs
  KeepRosariesModule.cs
  MaxHealthModule.cs
  DamageTakenModule.cs
```

**The boundary that matters:** the UI never knows what a module *does*. It reads
`DisplayName`, a list of settings with types and ranges, a `Status`, and `LastFired` — then
renders. Adding a fifth tweak touches no UI code. `Theme.cs` can be rewritten without
touching a single Harmony patch.

**Rules are pure.** Each module's decision logic is a static function over primitives with no
Unity types, so it is unit-testable off-game. The Harmony patch is a thin shim: read game
state, call the rule, write it back.

## Hook targets

Confirmed present in the shipped `Assembly-CSharp.dll` (game build `1.0.30000`) by symbol
extraction on 2026-08-08.

| Module | Primary targets | Notes |
|---|---|---|
| Return to death | `HeroRespawned`, `GetRespawnInfo`, `PrepareRespawnHere` | Record scene + position on death; transition on respawn |
| Keep rosaries | `AddToCocoonList`, `ReturnCocoon`, `FlingRosaries` | Silksong still names the economy `Geo` internally |
| Max health | `CurrentMaxHealth`, `BoundMaxHealth`, `AddToMaxHealth` | See verification step below |
| Damage taken | `TakeDamage`, `TakeDamageFromDamager` | Scale incoming amount in a prefix |

All targets live in `HookTargets.cs` and are consumed by both the runtime modules and the
test project. They are never duplicated.

**Naming note:** rosaries are `Geo` in code (`AddGeo`, `AddGeoQuietly`, `MegaFlingGeo`)
alongside `RosaryCache`. The Hollow Knight rename was cosmetic; the underlying economy code
kept its original names. UI says rosaries, code hooks `Geo`.

## Data flow

```
STARTUP
  Plugin.Awake
    └─ ModuleRegistry.DiscoverAll()
         ├─ module.BindConfig(ConfigFile)   → ConfigEntry<T> per setting
         └─ module.TryApply(harmony)        → patch, record Status

RUNTIME  (per frame, only while the window is open)
  F8 → TweakWindow.OnGUI → reads registry → draws

CHANGE
  slider drag → ConfigEntry.Value = x → BepInEx auto-writes .cfg
  patch body reads ConfigEntry.Value at execution time
```

**Patches read config values when they run and never cache them.** There is no Apply button
and no re-patching; a slider drag takes effect on the next hit taken.

Settings persist to `BepInEx/config/com.dloizides.silksongtweaks.cfg` in standard BepInEx
format, so the mod remains fully configurable by text editor if the UI ever fails to draw.

## Error handling

Three states, rendered differently:

| Status | Meaning | UI |
|---|---|---|
| `Active` | Patched and enabled | Normal, interactive |
| `Disabled` | Patched, user turned it off | Normal, toggle off |
| `Unavailable(reason)` | Target not found, or patching threw | Greyed, reason shown |

Rules:

1. **Disabling never un-patches.** The patch stays installed and short-circuits on its first
   line (`if (!_enabled.Value) return;`). Harmony runtime unpatching is a known source of
   instability; a boolean read is not.
2. **Every patch attempt is guarded.** A missing target or a throwing patch marks that module
   `Unavailable` and leaves the other three untouched.
3. **Every patch body is guarded.** A throwing prefix on `TakeDamage` would break the damage
   pipeline for the whole game. On exception a module logs once and self-disables.
4. **`OnGUI` is guarded.** An exception there fires every frame — thousands of log lines a
   minute and visible frame drops, which reads to a user as the game breaking. On repeated
   failure the UI disables itself and logs once.
5. **`Plugin.Awake` never throws.** Worst case is a window that opens and explains that all
   four modules are unavailable, and why.

## UI

Single IMGUI window, default hotkey `F8` (rebindable), sections mirroring the config file:

```
┌─ SILKSONG TWEAKS ─────────── [F8] ─┐
│  DEATH                             │
│   Return to death spot     [ ON ]  │
│   Countdown          ◄──●───►  5s  │
│   Keep rosaries            [ ON ]  │
│  HORNET                            │
│   Max masks          ◄────●─►   8  │
│   Damage taken       ◄●─────►  1x  │
│            [ Reset all ]           │
│  Credits                           │
└────────────────────────────────────┘
```

The sketch above is illustrative and abbreviated — it shows the primary control per
behaviour. Secondary settings from the scope table (cancel key, cocoon keep fraction, cocoon
stays in place) render in the same sections as additional rows.

Each module row shows its status and a **last fired** timestamp. This is the direct answer to
the ReBack ambiguity: whether a behaviour is actually running becomes a glance at the panel
rather than log archaeology.

The panel warns if `ReBack.dll`, `com.blueraja.rosaries_never_permanently_lost.dll`, or
`CustomDifficulty/` are still present in `plugins/`.

## Testing

**1 — Pure logic, unit tested off-game (xUnit).**
`DamageRules.Scale`, `HealthRules.Resolve`, `CocoonRules.Merge` are static functions over
primitives. Edge cases live here: clamping at zero, negative multipliers, integer
truncation, 20 masks with shard bonuses on top.

**2 — Hook-target existence test.**
A test loads the installed `Assembly-CSharp.dll` via `MetadataLoadContext` and asserts every
entry in `HookTargets.cs` exists with the expected signature. This turns "game patched, mod
silently broken" into a failing test that names the missing method, without launching the
game. It is only valid because `HookTargets.cs` is the single source of truth — a test with
its own copy of the list would verify a duplicate and manufacture false confidence.

**3 — In-game observability.**
Each module logs a structured line when it fires and surfaces `LastFired` in the panel.

**4 — Manual smoke checklist.** Short and written down: die once, confirm four things.

## Distribution

- Thunderstore package declaring `silksong_modding-BepInExPack_Silksong` as a dependency, so
  r2modman and Gale install BepInEx automatically.
- Manual install path documented in the README: drop the folder into `BepInEx/plugins/`.
- Defaults ship sensible; the mod is useful with zero configuration.

## Licence and attribution

MIT. The project is an **independent implementation** built only against `Assembly-CSharp.dll`
and public BepInEx/Harmony APIs. The three prior mods are **not** decompiled and none of their
code is copied — behaviour concepts are not copyrightable, their code is, and keeping that
line clean is what leaves this project cleanly MIT-licensable.

Credit is given in the README and an in-game Credits panel:

- **Xiaohai (XiaohaiMod)** — ReBack, for the return-to-death-location idea
- **BlueRaja** — rosaries never permanently lost, for cocoon merging
- **Ericky1694** — CustomDifficulty, for health and damage tuning

## Verification steps for implementation

These are resolved at implementation time; each has a defined fallback so none blocks design.

1. **Is `CurrentMaxHealth` a property or a field?** If it is a computed property, a postfix on
   its getter gives runtime-only health with a single patch and no re-assertion. If it is a
   field, fall back to re-asserting `maxHealth` after save load, bench rest, mask shard
   pickup, and scene load — more patches, same external behaviour.
2. **Does the return-to-death behaviour work at all on build `1.0.30000`?** ReBack's silence
   over three deaths is unexplained. Implement this module first and confirm with logging
   before building the others on the assumption it is achievable.
3. **What licences do the three prior mods carry?** Needed only so the README describes them
   accurately. Does not affect our licence, since no code is copied.
