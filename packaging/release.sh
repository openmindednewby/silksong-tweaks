#!/usr/bin/env bash
# One command to cut a release.
#
#   ./packaging/release.sh 1.0.1
#
# Does, in order, stopping at the first failure:
#   1. refuses a dirty tree or an existing tag
#   2. writes the version into manifest.json AND the csproj (single source: the argument)
#   3. builds + runs the tests
#   4. packages the Thunderstore zip
#   5. commits, tags, pushes
#   6. creates a GitHub Release with the zip attached
#   7. publishes to Thunderstore if `tcli` is installed, else prints the upload URL
#
# WHY THIS IS NOT A GITHUB ACTION
# The plugin compiles against Assembly-CSharp.dll and UnityEngine.* — Team Cherry's
# copyrighted game files. Putting them in the repo so a runner could build would mean
# redistributing the game. So the BUILD must happen on a machine that owns Silksong;
# everything after it is automated here.
set -euo pipefail

version="${1:-}"
if [ -z "$version" ]; then
  echo "usage: ./packaging/release.sh <version>   e.g. 1.0.1" >&2
  exit 1
fi

if ! printf '%s' "$version" | grep -qE '^[0-9]+\.[0-9]+\.[0-9]+$'; then
  echo "version must be MAJOR.MINOR.PATCH (Thunderstore requires all three)" >&2
  exit 1
fi

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

# A dirty tree means the built DLL would not correspond to any commit — the same rule
# the SaaS deploy pipeline enforces, and for the same reason.
if [ -n "$(git status --porcelain)" ]; then
  echo "working tree is dirty. Commit or stash first." >&2
  git status --short >&2
  exit 1
fi

if git rev-parse "v$version" >/dev/null 2>&1; then
  echo "tag v$version already exists. Thunderstore also refuses duplicate versions." >&2
  exit 1
fi

echo "==> setting version $version"
python - "$version" <<'PY'
import json, re, sys, pathlib
version = sys.argv[1]

manifest = pathlib.Path("packaging/thunderstore/manifest.json")
data = json.loads(manifest.read_text(encoding="utf-8"))
data["version_number"] = version
manifest.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")

csproj = pathlib.Path("src/SilksongTweaks/SilksongTweaks.csproj")
text = csproj.read_text(encoding="utf-8")
text = re.sub(r"<Version>[^<]*</Version>", f"<Version>{version}</Version>", text)
csproj.write_text(text, encoding="utf-8")

print("  manifest.json and csproj set to", version)
PY

echo "==> tests"
dotnet test tests/SilksongTweaks.Tests -v q --nologo

echo "==> package"
./packaging/build-package.sh

zip="packaging/dist/SilksongTweaks-$version.zip"
[ -f "$zip" ] || { echo "expected $zip" >&2; exit 1; }

echo "==> commit + tag"
git add -A
git commit -q -m "release: v$version"
git tag -a "v$version" -m "v$version"
git push -q origin main
git push -q origin "v$version"

echo "==> GitHub Release"
notes="$(awk '/^## /{n++} n==1{print} n>1{exit}' CHANGELOG.md 2>/dev/null || echo "See CHANGELOG.md")"
gh release create "v$version" "$zip" --title "v$version" --notes "$notes"

echo "==> Thunderstore"
if command -v tcli >/dev/null 2>&1; then
  if [ -z "${TCLI_AUTH_TOKEN:-}" ]; then
    echo "  TCLI_AUTH_TOKEN not set — skipping automatic publish."
    echo "  Create a service account token in your Thunderstore team settings."
  else
    tcli publish --file "$zip"
    echo "  published"
  fi
else
  echo "  tcli not installed. Either:"
  echo "    dotnet tool install -g tcli"
  echo "  or upload manually:"
  echo "    https://thunderstore.io/c/hollow-knight-silksong/create/"
  echo "  file: $root/$zip"
fi

echo
echo "done: v$version"
