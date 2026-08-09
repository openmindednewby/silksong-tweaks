# Development

Everything needed to build, test, debug and release this mod — including the traps that cost
real time, so nobody has to rediscover them.

## Prerequisites

| Need | Why |
|---|---|
| .NET SDK 8 or newer | Builds the plugin and the tests |
| Hollow Knight: Silksong, installed | Supplies the reference assemblies. **The mod cannot be built without it.** |
| BepInEx 5 in the game folder | Supplies `BepInEx.dll` and `0Harmony.dll` |

The csproj defaults to `D:\SteamLibrary\steamapps\common\Hollow Knight Silksong`. Elsewhere:

```bash
dotnet build src/SilksongTweaks -c Release -p:GameDir="C:\path\to\Hollow Knight Silksong"
```

## Build and test

```bash
dotnet build src/SilksongTweaks -c Release
dotnet test  tests/SilksongTweaks.Tests
```

Two kinds of test, and the second is the important one:

- **Rule tests** — pure functions over primitives (`DamageRules`, `HealthRules`, `CocoonRules`).
  All the arithmetic edge cases live here: clamping, negatives, overflow.
- **Hook-target tests** — load the *installed* `Assembly-CSharp.dll` through `MetadataLoadContext`
  and assert every entry in `HookTargets.cs` still exists. **Run these after any Silksong update.**
  A red test names the member that moved.

`HookTargets.cs` is compiled into both the plugin and the test project via `<Compile Include>`.
That is deliberate: a test with its own copy of the list would verify a duplicate and pass while
the shipped mod broke.

## Installing a local build

```bash
cp src/SilksongTweaks/bin/Release/netstandard2.1/SilksongTweaks.dll \
   "$GAME/BepInEx/plugins/"
```

**The game must be closed.** Windows locks a loaded assembly, so the copy silently fails or
errors while Silksong is running.

Startup log lives at `BepInEx/LogOutput.log`. A healthy start looks like:

```
[Silksong Tweaks] [ReturnToDeath] hooked OK
[Silksong Tweaks] [KeepRosaries] hooked OK
[Silksong Tweaks] [MaxHealth] restored max-health invariant at 4 site(s)
[Silksong Tweaks] [MaxHealth] hooked OK
[Silksong Tweaks] [DamageTaken] hooked OK
[Silksong Tweaks] 4/4 tweak(s) hooked successfully
```

## ApiInspector — read the game instead of guessing

`tools/ApiInspector` reads `Assembly-CSharp.dll` **without running it**. Nearly every correct
decision in this project came from it, and nearly every bug came from skipping it.

```bash
MANAGED="D:/SteamLibrary/steamapps/common/Hollow Knight Silksong/Hollow Knight Silksong_Data/Managed"

# What members does a type have?
dotnet run --project tools/ApiInspector -- dump "$MANAGED" '^PlayerData$' 'health'

# Who touches a member, and do they READ or WRITE it?
dotnet run --project tools/ApiInspector -- xref "$MANAGED" '^maxHealth$'

# Disassemble a method body.
dotnet run --project tools/ApiInspector -- il "$MANAGED" PlayerData '^AddHealth$'
```

`xref` answers the question that matters most when a patch "does nothing": *does the game read
the member I hooked, or a different one?* `il` then shows exactly how.

## Releasing

```bash
TCLI_ENV_FILE=/path/to/.env.local ./packaging/release.sh 1.0.1
```

Bumps the version in **both** `manifest.json` and the csproj, runs the tests, packages the zip,
commits, tags, pushes, creates a GitHub Release with the zip attached, and publishes to
Thunderstore.

It refuses to run on a dirty tree (a released DLL matching no commit is unreproducible) and on
an existing tag (Thunderstore rejects duplicate versions — better to fail before the upload).

**Thunderstore token**, resolved most-explicit-first:

1. `TCLI_AUTH_TOKEN`
2. `THUNDER_STORE_SERVICE_ACCOUNT_ACCESS_TOKEN`
3. the same key read out of the file at `$TCLI_ENV_FILE`

Create it under your Thunderstore **team → Service Accounts**. It is shown exactly once. The
path is passed in rather than hardcoded so this public repo names neither a value nor a location.

```bash
dotnet tool install -g tcli     # one-time
```

Versions are immutable on Thunderstore and must be `MAJOR.MINOR.PATCH`.

## Traps, all of them hit for real

**The build cannot run in CI.** It compiles against `Assembly-CSharp.dll` and `UnityEngine.*`,
which are Team Cherry's copyrighted files. Committing them so a runner could build would be
redistributing the game. Releases are built on a machine that owns Silksong; everything after
the build is automated.

**Target `netstandard2.1`, not 2.0.** Silksong runs Unity 6000.0.50, whose assemblies target
2.1. Targeting 2.0 fails with `CS1705`.

**Never hook a compiler-generated member.** Iterator state machines are named like
`HeroController/<Die>d__1101`, and that number is regenerated whenever the game is rebuilt.
A hook onto one breaks *silently* on any patch. Hook the enclosing method — a prefix there runs
when the enumerator is created, before any body code. A test enforces this rule.

**Silksong's health is event-driven, not polled.** Nothing continuously asks "how many masks
now?" — things are told once and remember. Three separate bugs came from this: healing read a
field we had not patched, damage tripped a top-up branch, and the HUD kept a cached shape. If
you change a value, also send whatever announcement the game would have sent
(`HeroController.MaxHealth()` fires `HeroCtrl-MaxHealth` at the HUD FSM).

**Do not break the game's invariants.** `CurrentMaxHealth` and `maxHealth` are equal in vanilla,
and the game uses them interchangeably *within a single method*. Raising only the property made
the game take branches written for a different situation. The fix was to restore the invariant
with a transpiler, not to patch more reads.

**`ldstr` literals are UTF-16.** Grepping the DLL as text finds type and member names (the
`#Strings` heap is UTF-8) but never string literals, which live UTF-16 in `#US`. Use the
`il` mode instead of concluding a string is absent.

**`convert` on Windows is not ImageMagick.** `C:\Windows\system32\convert` is the FAT→NTFS
filesystem converter. Use Python/PIL for image work. Likewise there is no `zip` on a stock
Windows box, so `build-package.sh` falls back to `python -m zipfile`.

**Disabling a tweak must not un-patch it.** Harmony's runtime unpatching is a known source of
instability. Patches stay installed and short-circuit on a boolean. Verified empirically: with
all four applied but disabled, the game boots and plays normally.

**An exception in `OnGUI` fires every frame.** Thousands of log lines a minute and a visible
framerate collapse that reads to a user as the *game* breaking. The draw call is wrapped and
self-disables after a failure.

## Project layout

```
src/SilksongTweaks/
  Plugin.cs            BepInEx entry, hotkeys, pause-while-open
  ITweakModule.cs      what the UI is allowed to know about a tweak
  ModuleRegistry.cs    applies modules independently so one failure is isolated
  HookTargets.cs       SINGLE source of truth, shared with the tests
  Rules/               pure decision logic, unit tested off-game
  Modules/             one file per tweak
  Ui/                  Theme (all styling), Widgets, TweakWindow, PanelInput, Conflicts
tests/                 rule tests + hook-target verification
tools/ApiInspector/    dump / xref / il over the game assembly
packaging/             Thunderstore manifest, build-package.sh, release.sh
```
