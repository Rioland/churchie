#!/usr/bin/env bash
# Wraps the published macOS build in a proper Churchie.app bundle, zips it, and builds
# the .pkg installer (build/make-macos-pkg.sh).
# Runs on the macOS GitHub runner, and locally via build/install-mac-app.sh.
#   usage: make-macos-app.sh <publish-folder> <rid> <version>
# Set KEEP_APP=1 to leave dist/Churchie.app in place instead of zipping it.
set -euo pipefail

PUBLISH_DIR="$1"
RID="$2"
VERSION="${3:-0.1.0}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/dist"
APP="$OUT/Churchie.app"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH_DIR"/. "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/Churchie"

# App icon (.icns) from the 1024px PNG
ICONSET="$OUT/churchie.iconset"
rm -rf "$ICONSET" && mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
  sips -z $size $size "$ROOT/build/churchie-1024.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z $double $double "$ROOT/build/churchie-1024.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/churchie.icns"
rm -rf "$ICONSET"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Churchie</string>
  <key>CFBundleDisplayName</key><string>Churchie</string>
  <key>CFBundleIdentifier</key><string>com.rioland.churchie</string>
  <key>CFBundleVersion</key><string>${VERSION}</string>
  <key>CFBundleShortVersionString</key><string>${VERSION}</string>
  <key>CFBundleExecutable</key><string>Churchie</string>
  <key>CFBundleIconFile</key><string>churchie.icns</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSMicrophoneUsageDescription</key><string>Churchie uses the microphone to record sermons and messages on the Record tab.</string>
</dict>
</plist>
PLIST

# Ad-hoc signature so Apple Silicon Macs will run it (not notarised — see README).
codesign --force --deep --sign - "$APP"

if [ "${KEEP_APP:-0}" = "1" ]; then
  echo "Created $APP"
  exit 0
fi

cd "$OUT"
ditto -c -k --keepParent Churchie.app "Churchie-${RID}.zip"
echo "Created $OUT/Churchie-${RID}.zip"
bash "$ROOT/build/make-macos-pkg.sh" "$APP" "$RID" "$VERSION"
rm -rf "$APP"
