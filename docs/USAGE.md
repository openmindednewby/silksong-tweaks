# How to use Silksong Tweaks

Press **`F8`** in game to open the panel. Everything changes instantly — there is no Apply
button and no restart.

---

## The four tweaks

### Return to death spot

After you respawn at a bench, a countdown appears on screen and Hornet travels back to exactly
where she died.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Turn the whole tweak off |
| Countdown | 5.0s | How long you get to change your mind |
| Cancel key | `N` | Press during the countdown to stay at the bench |

**The countdown prompt appears even with the panel closed**, so you never have to open a menu
mid-respawn. Press the cancel key and you simply stay put.

### Keep rosaries

Normally, dying a second time before recovering your cocoon destroys the first one and its
rosaries are gone for good. This folds the old cocoon into the new one instead.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Turn the whole tweak off |
| Keep from old cocoon | 100% | How much of the previous cocoon survives |

Set it to **0%** and you get exactly vanilla behaviour. **75%** keeps a real sting — you still
lose something each time — while removing the permanent loss. **100%** means rosaries are never
destroyed, only misplaced.

### Max masks

Raises Hornet's maximum health.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Turn the whole tweak off |
| Masks | 8 | Maximum masks, 1 to 20 |

**It never makes you weaker.** If you have earned more masks than the slider says, you keep
what you earned — so collecting mask shards always still does something.

**Your save file is never modified.** This works by intercepting the game's *reads* of max
health, not by writing a new value. Uninstall the mod and your save is exactly as it was, with
nothing to undo.

### Damage taken

Scales how much damage Hornet takes.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Turn the whole tweak off |
| Multiplier | 1.00x | 1 = vanilla, 0.5 = half damage, 0 = invulnerable |

A hit never silently becomes zero damage unless you set the multiplier to exactly 0 — a hit
that deals nothing but still knocks you back reads as a bug, not a mercy.

**Masks or damage?** They solve different problems. If long boss fights wear you down, raise
**masks**. If a single hit takes too much, lower **damage taken**.

---

## Reading the panel

Every tweak shows a status badge. This is the point of the mod:

| Badge | Meaning |
|---|---|
| `ACTIVE · fired 12s ago` | Working, and it has actually done something |
| `ACTIVE · not yet fired` | Hooked correctly, just hasn't been triggered yet |
| `OFF` | You switched it off |
| `UNAVAILABLE · <reason>` | It could not hook the game, with the reason |

`UNAVAILABLE` almost always means a Silksong update moved something. The other tweaks keep
working — one broken tweak never takes down the rest.

---

## Changing settings without the game

Everything lives in a plain text file:

```
BepInEx/config/com.dloizides.silksongtweaks.cfg
```

Edit it with any text editor and restart the game. The panel is a convenience on top of this
file, not the only way in — so if the UI ever fails to draw, you are not locked out.

---

## Turning things off

| Goal | How | Restart? |
|---|---|---|
| Skip one return, just this once | Press the cancel key during the countdown | no |
| Turn off one tweak | Its **Enabled** toggle in the panel | no |
| Turn off the whole mod | Rename `SilksongTweaks.dll` to `SilksongTweaks.dll.off` | yes |
| Play completely vanilla | Set `enabled = false` in `doorstop_config.ini` | yes |

The last one stops BepInEx loading at all, so nothing is injected — that is the only setting
that proves the game is running unmodded.

---

## Uninstalling

Delete `BepInEx/plugins/SilksongTweaks.dll`. That is the whole uninstall.

Nothing was written to your save and no game file was modified, so Steam's *Verify integrity of
game files* has nothing to repair.

---

## If something goes wrong

1. Open `BepInEx/LogOutput.log` and search for `Silksong Tweaks`. Startup prints one line per
   tweak saying whether it hooked.
2. If a teleport ever leaves you stuck in geometry, load your last save — and please report it
   with the log, since that is a real bug worth fixing.
3. Back up your saves before experimenting. They live in:
   `%USERPROFILE%\AppData\LocalLow\Team Cherry\Hollow Knight Silksong\`
