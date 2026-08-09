# Silksong Tweaks

Four quality-of-life tweaks for **Hollow Knight: Silksong**, in one mod with one panel.

Press **`F8`** in game. Everything applies instantly.

- **Return to death spot** — after respawning at a bench, travel back to where you died, on a
  cancellable countdown
- **Keep rosaries** — dying twice no longer destroys your first cocoon
- **Max masks** — raise Hornet's maximum health (1–20)
- **Damage taken** — scale incoming damage, from invulnerable to harder than vanilla

👉 **[Full usage guide](docs/USAGE.md)**

---

## Install

**With a mod manager (easiest).** Install from Thunderstore with r2modman or Gale. BepInEx is
pulled in automatically as a dependency — nothing else to do.

**By hand.**

1. Install [BepInEx 5 for Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/)
   — extract it, then copy the *contents* of the `BepInExPack` folder next to
   `Hollow Knight Silksong.exe`.
2. Run the game once so BepInEx creates its folders, then quit.
3. Drop `SilksongTweaks.dll` into `BepInEx/plugins/`.
4. Launch and press `F8`.

**Uninstall:** delete `SilksongTweaks.dll`. Nothing else is touched.

---

## Why another mod?

Because you cannot tell whether the others are working.

The mods this replaces will happily report that they loaded and then do nothing at all — and
from the outside, a working mod and a silently broken one look identical. This mod shows a
status per tweak: whether it hooked the game, and when it last actually did something.

| Badge | Meaning |
|---|---|
| `ACTIVE · fired 12s ago` | Working, and it has done something |
| `ACTIVE · not yet fired` | Hooked, not yet triggered |
| `OFF` | You switched it off |
| `UNAVAILABLE · <reason>` | Could not hook the game, and why |

A game update that breaks one tweak leaves the other three running, and says so.

---

## Design notes

- **Your save file is never written to.** Max health works by intercepting the game's *reads*,
  never by storing a new value. Uninstalling leaves a vanilla save with nothing to undo.
- **Nothing hooks compiler-generated code.** Iterator state machines like `<Die>d__1101` carry
  numbers the compiler regenerates on every build, so hooking them breaks silently on any game
  patch. Every hook targets a stable, named method.
- **One source of truth for hook targets.** `HookTargets.cs` is consumed by both the runtime and
  a test that reads your *installed* `Assembly-CSharp.dll` and fails if any target has moved.
  Run it after a Silksong update and it names what broke.
- **Disabling never un-patches.** Patches stay installed and short-circuit on a boolean, because
  runtime un-patching is a known source of instability.

---

## Building

Requires the .NET SDK and an installed copy of Silksong, which supplies the reference
assemblies.

```bash
dotnet build src/SilksongTweaks -c Release
dotnet test  tests/SilksongTweaks.Tests
```

👉 **[Local co-op plan](docs/LOCAL-COOP-PLAN.md)** — how two people could play on one PC, and
why split-screen is not the route.

👉 **[Full development guide](docs/DEVELOPMENT.md)** — the ApiInspector tool, the release
process, and every trap worth knowing (why it can't build in CI, why hooking compiler-generated
iterators breaks silently, why Silksong's health is event-driven).

## Releasing

```bash
./packaging/release.sh 1.0.1
```

Bumps the version everywhere, tests, packages, tags, pushes, creates the GitHub Release and
publishes to Thunderstore. See [DEVELOPMENT.md](docs/DEVELOPMENT.md#releasing) for token setup.

---

## Credits

Silksong Tweaks is an **independent implementation**, written against the game's own API. None
of the mods below were decompiled and none of their code is used. They came first, they proved
these ideas were worth having, and they deserve the credit for them:

- **[Xiaohai (XiaohaiMod)](https://thunderstore.io/c/hollow-knight-silksong/p/XiaohaiMod/ReBack/)**
  — *ReBack*, for returning to the death location
- **[BlueRaja](https://thunderstore.io/c/hollow-knight-silksong/p/BlueRaja/rosaries_never_permanently_lost/)**
  — *rosaries never permanently lost*, for merging death cocoons
- **[Ericky1694](https://thunderstore.io/c/hollow-knight-silksong/p/Ericky1694/CustomDifficulty/)**
  — *CustomDifficulty*, for health and damage tuning

Built on [BepInEx](https://github.com/BepInEx/BepInEx) and
[HarmonyX](https://github.com/BepInEx/HarmonyX). Not affiliated with Team Cherry.

## Licence

[MIT](LICENSE).
