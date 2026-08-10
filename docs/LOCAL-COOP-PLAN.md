# Local co-op for Silksong — plan

**Goal:** two people, one PC, one sofa, two controllers.

**Status:** planned, nothing built. Phase 0 is a spike that can kill the whole idea in an hour,
which is exactly why it comes first.

---

## What already exists

| Mod | Licence | What it does |
|---|---|---|
| [SSMP](https://github.com/Extremelyd1/SSMP) v0.3.1 | **LGPL-2.1-or-later** | Networked co-op: Steam invites, matchmaking with NAT hole-punching, direct IP, LAN |
| [SilklessCoop](https://www.nexusmods.com/hollowknightsilksong/mods/73) | — | Networked co-op |

Neither does same-screen local play. SSMP's README states *"Playing on the same local network
works out of the box"*, which is the opening this plan walks through.

## Why true split-screen is not the plan

Measured, not assumed — via `tools/ApiInspector`:

```
HeroController.instance call sites : 1487
HeroController._instance           : static field
```

Silksong is built around exactly one hero: one `HeroController`, one `PlayerData`, one camera
rig, one save. A second `HeroController` cannot exist — 1,487 call sites would follow whichever
instance won the singleton race.

That is why SSMP renders remote players as **puppets**: sprite and animator driven by network
data, with only the local player being a real `HeroController`. True split-screen would mean
giving a puppet real physics, combat, dash, wall-cling, hook, pogo and silk arts — reimplementing
Hornet's controller — plus two cameras and viewport work. Months, and it fights the engine the
whole way.

**So: don't build a second player. Run a second game.**

---

## Phase 0 — Feasibility spike — **RUN 2026-08-10. Verdict: proceed.**

Two of the three questions passed. The one that failed did so for a reason this plan had
half-right and half-wrong, and it does not block Phase 1.

### (a) Two instances at once — **YES**, after one config change

Not because of Steam. Steam is a non-issue: both processes ran `SteamAPI_Init` independently and
both logged in as the same user. The blocker was **Unity's Force Single Instance**, declared as
`single-instance=` in `Hollow Knight Silksong_Data/boot.config`. The second process put up a
`Fatal error` dialog reading *"Another instance is already running"* and exited 0 before Unity
started — with BepInEx disabled, so neither BepInEx nor Steam was involved.

Remove that one line from `boot.config` and both instances run: two processes, ~730 MB each,
both to the title screen, both with Steam and BepInEx live. On a 24-thread / 30 GB box that is
comfortable. **The launcher in Phase 2 must strip `single-instance=` from the second copy's
`boot.config`** — it is a file inside the copied folder, so the original install is untouched.

### (b) SSMP with two clients from one machine — **NO, and a separate game folder does not fix it**

Determined by reading SSMP's source, not by running it. Two corrections to what this plan assumed:

- **Random collision is not the risk.** `AuthUtil.GenerateAuthKey` makes a 56-character key from a
  CSPRNG. Two independent copies will never collide by chance.
- **The IP address is not the risk either.** `ReplaceExistingSessionIfPresent` matches an incoming
  client against existing players on `UniqueClientIdentifier` *or* `AuthKey`, and for UDP that
  identifier is `EndPoint.ToString()` — IP **and port**. Two loopback clients get different
  ephemeral ports, so that branch does not fire.
- **The shared config file is the risk.** `FileUtil.GetConfigPath()` returns
  `<save dir>/ssmp`, and the save dir is `%LOCALAPPDATA%Low/Team Cherry/Hollow Knight
  Silksong/<SteamUserId>/` — keyed on the Steam user, **not on the install folder**. Both
  instances therefore load the same `modsettings.json` and present the *same* `AuthKey`. The
  `AuthKey` branch fires, and player 2 connecting **kicks player 1** rather than joining them.

So copying the game folder — the mitigation this plan proposed — changes nothing, because the
auth key does not live in the game folder. Any fix has to give the second instance a different
`AuthKey`: a different Steam account, a different Windows user, or swapping the key in
`modsettings.json` between the two launches.

### (c) Do both instances grab all gamepads — **yes, no exclusivity. Phase 1 is needed.**

Both processes loaded `xinput1_3.dll` and `Windows.Gaming.Input.dll` at the same time, and
neither loaded `dinput8.dll` — so nothing is using DirectInput's exclusive mode. While both games
were running, a *third* process read `XInputGetState` slot 0 successfully. Three concurrent
readers of one pad: nothing is claiming it.

**Not verified:** only one controller is connected to this machine, so "pad 2 drives instance 2
and not instance 1" has not been observed. Confirming that needs two pads and a human.

## Phase 1 — Controller Lock (~1 day)

The known snag: both instances read **all** connected gamepads, so each Hornet answers to both
controllers.

Scope: restrict one game instance to one joystick.

- Config: `JoystickIndex` (0 = all, 1 = first pad, 2 = second pad), defaulting to 0 so installing
  it changes nothing for existing users
- **Not legacy Unity input.** Silksong does not read gamepads through `UnityEngine.Input`, so
  `KeyCode.Joystick1Button0` and `"Joystick1Axis1"` have nothing to filter. It uses **InControl**,
  compiled into `Assembly-CSharp` (namespace `InControl`; `SharpDX.DirectInput.dll` beside it is
  InControl's dependency). Measured, not assumed.
- The filter point is InControl's own API. `PlayerActionSet.Update` resolves one device per frame
  as `Device ?? FindActiveDevice()` and hands it to every action; `FindActiveDevice` only
  considers devices in `IncludeDevices` once that list is non-empty. A one-entry `IncludeDevices`
  is therefore a hard restriction — still **filtering existing input, not reimplementing it**.
- The game never writes `IncludeDevices` itself (zero IL references), so nothing fights us.
- Keyboard is unaffected **by construction**: gamepad bindings are `DeviceBindingSource`, which
  reads the device it is handed, while `KeyBindingSource.GetState` ignores the device argument
  entirely and reads the keyboard directly.
- One hook covers everything: `Update` is declared only on the base `PlayerActionSet` and no
  subclass overrides it, so the patch catches `HeroActions` (gameplay *and* menus) and
  `PreMenuInputModuleActions` alike.

**Where it lives:** build it as a fifth module inside Silksong Tweaks first. The module system,
config binding, status reporting and UI come free, so the marginal cost is one file. Extract it
to a standalone mod only if it proves useful to people who do not want the tweaks — *extract on
the second use, not the first*.

**Licence:** Controller Lock does not link SSMP or use any of its code — it only filters input
inside its own process. It therefore stays **MIT**, like the rest of this repo. Keep it that way:
the moment we link SSMP, LGPL obligations attach.

**Definition of done:** two instances, two controllers, each Hornet responds only to its own pad,
and both players can move at the same time without input bleed.

## Phase 2 — Launcher polish (optional, ~1 day)

Only worth doing if Phase 1 lands and the experience is actually fun.

- A script that clones the game folder, applies the right `JoystickIndex` to each copy, and
  launches both windows positioned side by side
- Document the whole flow in `docs/LOCAL-COOP.md` for users

## Phase 3 — Upstream local support to SSMP (days–weeks)

Better UX than two windows, but a different kind of work: collaboration, not just code.

**Approach:**

1. Open an issue on [SSMP](https://github.com/Extremelyd1/SSMP/issues) describing local co-op
   and asking whether the maintainer wants it upstream **before writing anything**. A rejected
   PR after two weeks of work is the failure mode to avoid.
2. Read [CONTRIBUTING.md](https://github.com/Extremelyd1/SSMP/blob/main/CONTRIBUTING.md) and
   follow their conventions rather than ours.
3. Likely shape: a "local player" input source alongside the network one, so a second puppet is
   driven by a gamepad instead of by packets. SSMP already renders and animates remote players —
   the work is feeding that pipeline from local input, not building it.
4. Note the camera problem honestly: with both players on one screen, one camera must frame both,
   or player 2 is capped to the host's view. This is the part most likely to make the maintainer
   say no, so raise it in the issue rather than discovering it in review.

**Licence:** contributions to SSMP are **LGPL-2.1-or-later**, not MIT. That is fine and normal —
but it means code written for upstream cannot simply be copied back into this MIT repo. Decide
where a piece of work lives *before* writing it.

---

## Order and rationale

```
Phase 0  spike            1 hour     can kill everything
Phase 1  Controller Lock  ~1 day     playable local co-op
Phase 2  launcher         ~1 day     optional polish
Phase 3  upstream         weeks      better UX, needs buy-in
```

Phases 0–2 are entirely within our control and produce something playable. Phase 3 depends on
another maintainer, so it must never be on the critical path for having local co-op working.

## Open questions

1. ~~**Does SSMP tolerate two clients from one machine?**~~ **No** — both instances present the
   same `AuthKey` from a save-dir-scoped `modsettings.json`, and the second connection kicks the
   first. See Phase 0 (b). Copying the game folder does not help.
2. ~~**Does Silksong grab exclusive access to gamepads?**~~ **No** — nothing claims a pad
   exclusively, so the filtering in Phase 1 is required. See Phase 0 (c).
3. **What is SSMP's minimum viable topology for localhost?** Direct IP to `127.0.0.1:26960`,
   avoiding the matchmaking service entirely. Untested — blocked behind question 5.
4. **Is two-window co-op actually fun?** Worth an honest answer after Phase 1 before investing in
   Phase 2 or 3.
5. **How do we give instance 2 a different SSMP `AuthKey`?** The blocker from Phase 0 (b). The
   cheapest candidate is a launcher step that swaps the key in `modsettings.json` between the two
   launches; the robust one is upstreaming a per-instance config path into SSMP. Untested.
6. **Does one pad really drive only one Hornet?** Phase 1's definition of done. Needs two
   controllers and a human; this machine has one pad.
