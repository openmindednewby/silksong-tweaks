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

## Phase 0 — Feasibility spike (1 hour, can kill the plan)

Answer one question: **will Silksong run twice on this machine at once?**

1. Copy the game folder to a second location (BepInEx included).
2. Launch each directly from `Hollow Knight Silksong.exe`, not through Steam. The BepInEx pack
   already ships `steam_appid.txt`, which is what allows a direct launch to satisfy the Steam
   API without going through the client's single-instance guard.
3. Confirm both windows run and both reach the main menu.
4. In instance A, host via SSMP. In instance B, join `127.0.0.1:26960`.

**Kill criteria — abandon or rethink if:**

- The second instance refuses to start or Steam kills it
- SSMP rejects two clients from one machine (auth key collision — each player uses a
  locally-generated key, so a shared install directory may produce the same key; a separate
  game folder per instance likely avoids this, and that is worth checking here)
- Performance is unusable with two instances at once

**Do not write any code before this passes.**

## Phase 1 — Controller Lock (~1 day)

The known snag: both instances read **all** connected gamepads, so each Hornet answers to both
controllers.

Scope: restrict one game instance to one joystick.

- Config: `JoystickIndex` (0 = all, 1 = first pad, 2 = second pad)
- Unity's legacy input already exposes per-joystick buttons (`Joystick1Button0`,
  `Joystick2Button0`) and per-joystick axes, so this is **filtering existing input, not
  reimplementing it**
- Ignore input from every other joystick index; leave keyboard alone so one player can use it

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

1. **Does SSMP tolerate two clients from one machine?** Auth keys are locally generated; two
   copies of the same install folder may collide. Resolved in Phase 0.
2. **Does Silksong grab exclusive access to gamepads?** If an instance claims a pad exclusively,
   filtering is unnecessary — but if it claims *all* of them, Phase 1 changes shape.
3. **What is SSMP's minimum viable topology for localhost?** Direct IP to `127.0.0.1` should be
   simplest, avoiding the matchmaking service entirely.
4. **Is two-window co-op actually fun?** Worth an honest answer after Phase 1 before investing in
   Phase 2 or 3.
