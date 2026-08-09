#!/usr/bin/env bash
# Builds the Thunderstore upload zip.
#
#   ./packaging/build-package.sh
#
# Produces packaging/dist/SilksongTweaks-<version>.zip with the layout Thunderstore requires:
#   manifest.json   icon.png   README.md   CHANGELOG.md   BepInEx/plugins/SilksongTweaks.dll
#
# Thunderstore reads the version from manifest.json, and REFUSES a re-upload of an existing
# version — so bump it there before packaging, not after the upload fails.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
src="$root/src/SilksongTweaks"
pkg="$root/packaging/thunderstore"
dist="$root/packaging/dist"

version="$(grep -oE '"version_number"[[:space:]]*:[[:space:]]*"[^"]+"' "$pkg/manifest.json" \
           | grep -oE '[0-9]+\.[0-9]+\.[0-9]+')"

if [ -z "$version" ]; then
  echo "could not read version_number from manifest.json" >&2
  exit 1
fi

if [ ! -f "$pkg/icon.png" ]; then
  echo "MISSING: $pkg/icon.png — Thunderstore requires exactly 256x256 PNG" >&2
  exit 1
fi

echo "building $version"
dotnet build "$src" -c Release --nologo

staging="$dist/staging"
rm -rf "$staging"
mkdir -p "$staging/BepInEx/plugins"

cp "$pkg/manifest.json"          "$staging/"
cp "$pkg/icon.png"               "$staging/"
cp "$root/README.md"             "$staging/"
cp "$root/CHANGELOG.md"          "$staging/"
cp "$root/LICENSE"               "$staging/"
cp "$src/bin/Release/netstandard2.1/SilksongTweaks.dll" "$staging/BepInEx/plugins/"

out="$dist/SilksongTweaks-$version.zip"
rm -f "$out"

# `zip` is not present on a stock Windows/Git-Bash box, and python always is here.
if command -v zip >/dev/null 2>&1; then
  ( cd "$staging" && zip -qr "$out" . )
else
  python - "$staging" "$out" <<'PY'
import os, sys, zipfile
staging, out = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for root, _, files in os.walk(staging):
        for f in files:
            full = os.path.join(root, f)
            # Thunderstore requires manifest.json/icon.png/README.md at the ARCHIVE ROOT,
            # so paths are stored relative to the staging dir with forward slashes.
            z.write(full, os.path.relpath(full, staging).replace(os.sep, "/"))
PY
fi

rm -rf "$staging"

echo "packaged: $out"
echo "upload at https://thunderstore.io/c/hollow-knight-silksong/create/"
