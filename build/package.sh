#!/usr/bin/env bash
# Builds the download package for one platform into dist/.
#   usage: build/package.sh <rid> [version]
#   rid:   win-x64 | linux-x64 | osx-arm64 | osx-x64
#
# Produces (the website's download links use these exact names):
#   dist/Churchie-win-x64.zip        (a "Churchie" folder with Churchie.exe)
#   dist/Churchie-linux-x64.tar.gz   (a "Churchie" folder with the Churchie program)
#   dist/Churchie-osx-arm64.zip      (Churchie.app for Apple Silicon Macs)
#   dist/Churchie-osx-x64.zip        (Churchie.app for Intel Macs)
#
# The macOS packages must be built on a Mac (they are signed with codesign).
# Used by GitHub Actions and by the Mac "Make Downloads" launcher.
set -euo pipefail

RID="${1:?usage: package.sh <rid> [version]}"
VERSION="${2:-0.1.0}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH="$ROOT/publish/${RID}"
STAGE="$ROOT/publish/stage-${RID}/Churchie"
DIST="$ROOT/dist"
mkdir -p "$DIST"

echo "== Publishing Churchie ${VERSION} for ${RID}..."
rm -rf "$PUBLISH"
dotnet publish "$ROOT/src/Churchie.App/Churchie.App.csproj" \
  -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None \
  -p:Version="$VERSION" \
  -v minimal -o "$PUBLISH"

# Zip a folder's contents, with whatever zip tool this computer has.
make_zip() { # <folder-containing-item> <item> <zip-path>
  local parent="$1" item="$2" out="$3"
  rm -f "$out"
  if command -v zip >/dev/null 2>&1; then
    (cd "$parent" && zip -qr "$out" "$item")
  elif command -v 7z >/dev/null 2>&1; then
    (cd "$parent" && 7z a -tzip -bd "$out" "$item" >/dev/null)
  else
    powershell -NoProfile -Command "Compress-Archive -Path '$parent/$item' -DestinationPath '$out' -Force"
  fi
}

case "$RID" in
  win-*)
    rm -rf "$(dirname "$STAGE")" && mkdir -p "$STAGE"
    cp -R "$PUBLISH"/. "$STAGE/"
    make_zip "$(dirname "$STAGE")" Churchie "$DIST/Churchie-${RID}.zip"
    ;;
  linux-*)
    rm -rf "$(dirname "$STAGE")" && mkdir -p "$STAGE"
    cp -R "$PUBLISH"/. "$STAGE/"
    chmod +x "$STAGE/Churchie"
    tar -czf "$DIST/Churchie-${RID}.tar.gz" -C "$(dirname "$STAGE")" Churchie
    ;;
  osx-*)
    bash "$ROOT/build/make-macos-app.sh" "$PUBLISH" "$RID" "$VERSION"
    ;;
  *)
    echo "Unknown platform: $RID" >&2
    exit 1
    ;;
esac

echo "== Done: $(ls "$DIST"/Churchie-"${RID}".* )"
