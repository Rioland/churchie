#!/usr/bin/env bash
# Builds Churchie for this Mac and installs it as ~/Applications/Churchie.app,
# so it opens like any other app (Launchpad, Spotlight, Dock).
#   usage: build/install-mac-app.sh [version]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${1:-0.1.0}"
case "$(uname -m)" in
  arm64) RID=osx-arm64 ;;
  *)     RID=osx-x64 ;;
esac
PUBLISH="$ROOT/publish/$RID"
DEST="$HOME/Applications/Churchie.app"

echo "== Publishing Churchie ${VERSION} for ${RID}..."
rm -rf "$PUBLISH"
dotnet publish "$ROOT/src/Churchie.App/Churchie.App.csproj" \
  -c Release -r "$RID" --self-contained true \
  -p:Version="$VERSION" -v minimal -o "$PUBLISH"

echo "== Making Churchie.app..."
KEEP_APP=1 bash "$ROOT/build/make-macos-app.sh" "$PUBLISH" "$RID" "$VERSION"

mkdir -p "$HOME/Applications"
# Quit a running Churchie so its app bundle can be replaced
# (the schedule autosaves on every change, so nothing is lost).
pkill -x Churchie 2>/dev/null && sleep 1 || true
rm -rf "$DEST"
mv "$ROOT/dist/Churchie.app" "$DEST"
echo "== Installed ${DEST}"
