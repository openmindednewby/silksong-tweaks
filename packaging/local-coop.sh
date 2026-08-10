#!/usr/bin/env bash
# Launch two Silksong instances side by side for local co-op.
#
#   ./packaging/local-coop.sh
#   ./packaging/local-coop.sh --game "D:/SteamLibrary/steamapps/common/Hollow Knight Silksong"
#
# What it does and why each step is needed:
#
#   1. Unity refuses a second instance because of `single-instance=` in boot.config. That line is
#      removed, both games are launched, and it is RESTORED IMMEDIATELY — the game only reads
#      boot.config at startup, so the install ends up byte-identical to how it started.
#
#   2. Both instances share one install folder, so they share BepInEx's config and would lock
#      onto the SAME pad. SILKSONG_JOYSTICK is a per-PROCESS override read by ControllerLock, so
#      window 1 gets pad 1 and window 2 gets pad 2 without copying 7.7 GB of game.
#
#   3. SSMP stores its auth key under the SAVE directory, keyed on Steam user rather than install
#      folder, so both instances would present the SAME key — and SSMP treats that as a
#      reconnect, kicking player 1 when player 2 joins. Between the two launches the key is
#      blanked, and SSMP regenerates a fresh one on the second start entirely by itself:
#          if (!AuthUtil.IsValidAuthKey(_modSettings.AuthKey)) { ...GenerateAuthKey(); }
#      So no key is ever hand-crafted here; the game's own generator does it.
#
# Then: in window 1 host via SSMP, in window 2 join 127.0.0.1 on the port shown.
set -euo pipefail

GAME="D:/SteamLibrary/steamapps/common/Hollow Knight Silksong"
SETTLE_SECONDS=25

while [ $# -gt 0 ]; do
  case "$1" in
    --game) GAME="$2"; shift 2 ;;
    --settle) SETTLE_SECONDS="$2"; shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 1 ;;
  esac
done

EXE="$GAME/Hollow Knight Silksong.exe"
BOOT="$GAME/Hollow Knight Silksong_Data/boot.config"

[ -f "$EXE" ]  || { echo "game exe not found: $EXE" >&2; exit 1; }
[ -f "$BOOT" ] || { echo "boot.config not found: $BOOT" >&2; exit 1; }

if tasklist 2>/dev/null | grep -qi "Hollow Knight Silksong"; then
  echo "Silksong is already running. Close it first." >&2
  exit 1
fi

# --- SSMP presence -------------------------------------------------------------------------
if ! find "$GAME/BepInEx/plugins" -iname "*ssmp*" -print -quit 2>/dev/null | grep -q .; then
  echo "WARNING: SSMP does not appear to be installed in BepInEx/plugins."
  echo "         Both windows will still launch, but they cannot connect to each other."
  echo "         Install it from https://thunderstore.io/c/hollow-knight-silksong/p/SSMP/SSMP/"
  echo
fi

# --- locate SSMP's settings (save dir is keyed on Steam user, NOT the install folder) --------
SAVE_ROOT="$USERPROFILE/AppData/LocalLow/Team Cherry/Hollow Knight Silksong"
SETTINGS=""
if [ -d "$SAVE_ROOT" ]; then
  SETTINGS="$(find "$SAVE_ROOT" -name modsettings.json -path "*ssmp*" -print -quit 2>/dev/null || true)"
fi

# --- boot.config: remove the single-instance guard, ALWAYS restore ---------------------------
BOOT_BACKUP="$(mktemp)"
cp "$BOOT" "$BOOT_BACKUP"

restore_boot() {
  if [ -f "$BOOT_BACKUP" ]; then
    cp "$BOOT_BACKUP" "$BOOT"
    rm -f "$BOOT_BACKUP"
    echo "boot.config restored"
  fi
}
# A trap, so an interrupt or an error cannot leave the guard removed.
trap restore_boot EXIT INT TERM

grep -v '^single-instance=' "$BOOT_BACKUP" > "$BOOT"
echo "single-instance guard lifted (temporarily)"

# --- launch player 1 -------------------------------------------------------------------------
echo "launching window 1 (pad 1)..."
( cd "$GAME" && SILKSONG_JOYSTICK=1 "./Hollow Knight Silksong.exe" >/dev/null 2>&1 & )

echo "waiting ${SETTLE_SECONDS}s for it to read its auth key..."
sleep "$SETTLE_SECONDS"

# --- give player 2 a different auth key ------------------------------------------------------
if [ -n "$SETTINGS" ] && [ -f "$SETTINGS" ]; then
  python - "$SETTINGS" <<'PY'
import json, sys
path = sys.argv[1]
with open(path, encoding="utf-8") as f:
    data = json.load(f)
# Blanking it is enough: SSMP validates on startup and regenerates when invalid, so the second
# instance mints its own key with the game's own CSPRNG. We never invent a key here.
data["AuthKey"] = None
with open(path, "w", encoding="utf-8") as f:
    json.dump(data, f, indent=4)
print("auth key cleared; instance 2 will generate its own")
PY
else
  echo "NOTE: SSMP settings not found, so the auth key was not cleared."
  echo "      If player 2 kicks player 1 off, that is why — run SSMP once in a single instance"
  echo "      first so its settings file exists, then re-run this script."
fi

# --- launch player 2 -------------------------------------------------------------------------
echo "launching window 2 (pad 2)..."
( cd "$GAME" && SILKSONG_JOYSTICK=2 "./Hollow Knight Silksong.exe" >/dev/null 2>&1 & )

sleep 8
echo
echo "running instances: $(tasklist 2>/dev/null | grep -ci "Hollow Knight Silksong" || echo 0)"
echo
echo "NEXT:"
echo "  window 1  -> Start Multiplayer -> Host"
echo "  window 2  -> Start Multiplayer -> Join 127.0.0.1 on the port window 1 shows"
echo
echo "Each window is locked to its own pad via SILKSONG_JOYSTICK. Check the F8 panel's"
echo "Controller lock row if a pad drives the wrong Hornet."
