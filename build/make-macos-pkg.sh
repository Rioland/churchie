#!/usr/bin/env bash
# Builds a macOS installer package (.pkg) that puts Churchie.app in /Applications.
# Double-clicking it opens Apple's Installer: Continue, Install, done.
#   usage: make-macos-pkg.sh <path/to/Churchie.app> <rid> <version>
# Writes dist/Churchie-<rid>.pkg. Uses pkgbuild and productbuild, which come with macOS.
set -euo pipefail

APP="${1:?path to Churchie.app}"
RID="${2:?osx-arm64 or osx-x64}"
VERSION="${3:-0.1.0}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/dist"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# What goes in the package: just Churchie.app. ditto keeps its signature and permissions.
mkdir -p "$WORK/root"
ditto "$APP" "$WORK/root/Churchie.app"

# By default Installer "relocates" an update onto any other copy of Churchie it finds
# (for example one in Downloads). Turn that off so it always lands in /Applications.
pkgbuild --analyze --root "$WORK/root" "$WORK/component.plist" >/dev/null
/usr/libexec/PlistBuddy -c "Set :0:BundleIsRelocatable false" "$WORK/component.plist"

pkgbuild --root "$WORK/root" \
  --component-plist "$WORK/component.plist" \
  --identifier com.rioland.churchie \
  --version "$VERSION" \
  --install-location /Applications \
  "$WORK/Churchie.pkg" >/dev/null

# The Apple Silicon build can't run on Intel Macs, so its installer refuses them with a
# clear message. The Intel build also runs on Apple Silicon (through Rosetta).
case "$RID" in
  osx-arm64) ARCHES="arm64" ;;
  *)         ARCHES="x86_64,arm64" ;;
esac

# The installer's title, which Macs it accepts, and what it installs.
cat > "$WORK/distribution.xml" <<XML
<?xml version="1.0" encoding="utf-8"?>
<installer-gui-script minSpecVersion="2">
    <title>Churchie ${VERSION}</title>
    <options customize="never" require-scripts="false" hostArchitectures="${ARCHES}"/>
    <domains enable_localSystem="true"/>
    <volume-check>
        <allowed-os-versions>
            <os-version min="12.0"/>
        </allowed-os-versions>
    </volume-check>
    <choices-outline>
        <line choice="default">
            <line choice="com.rioland.churchie"/>
        </line>
    </choices-outline>
    <choice id="default"/>
    <choice id="com.rioland.churchie" visible="false">
        <pkg-ref id="com.rioland.churchie"/>
    </choice>
    <pkg-ref id="com.rioland.churchie" version="${VERSION}" onConclusion="none">Churchie.pkg</pkg-ref>
</installer-gui-script>
XML

mkdir -p "$OUT"
productbuild --distribution "$WORK/distribution.xml" --package-path "$WORK" \
  "$OUT/Churchie-${RID}.pkg" >/dev/null
echo "Created $OUT/Churchie-${RID}.pkg"
