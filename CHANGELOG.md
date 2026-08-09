# Changelog

## 1.0.0

First release. Four tweaks in one panel, opened with `F8` or the Back/View button on a controller.

- **Return to death spot** — travel back to where you died after respawning, on a cancellable
  countdown
- **Keep rosaries** — dying twice no longer destroys your first cocoon
- **Max masks** — raise maximum health from 1 to 20
- **Damage taken** — scale incoming damage, from invulnerable to harder than vanilla

Every tweak reports whether it actually hooked the game and when it last did something, so a
tweak that silently stopped working after a game update is visible rather than invisible.

Your save file is never written to. Max health works by intercepting the game's reads, so
uninstalling leaves a vanilla save with nothing to undo.
