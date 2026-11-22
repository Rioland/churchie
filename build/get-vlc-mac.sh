#!/usr/bin/env bash
# Puts the VLC video engine inside Churchie.app, so videos play on a Mac without
# installing anything else.
#
# VLC's NuGet package for Mac only has Intel code, so instead this takes the engine
# out of the official VLC app from videolan.org (one download per processor type),
# checks the download against VideoLAN's published checksum, and copies it into
#   Churchie.app/Contents/MacOS/libvlc/<rid>/lib/
# which is where LibVLCSharp looks for it. The layout copies VideoLAN's own packaging
# (code.videolan.org/videolan/libvlc-nuget, package-nuget-mac.sh):
#   lib/libvlc.dylib, lib/libvlccore.dylib   the engine
#   lib/vlc/plugins/                          decoders and outputs, found next to libvlccore
#   lib/vlc/share/hrtfs                       a small data folder some audio filters use
# Plugins under the GPL licence are left out, as in VideoLAN's LGPL package.
#
#   usage: get-vlc-mac.sh <path to Churchie.app> <rid: osx-arm64 | osx-x64>
# Downloads are kept in .cache/vlc so building again doesn't download again.
set -euo pipefail

APP="${1:?usage: get-vlc-mac.sh <Churchie.app> <rid>}"
RID="${2:?usage: get-vlc-mac.sh <Churchie.app> <rid>}"
VLC_VERSION="3.0.24"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CACHE="$ROOT/.cache/vlc"

case "$RID" in
  osx-arm64) ARCH=arm64 ;;
  osx-x64)   ARCH=intel64 ;;
  *) echo "get-vlc-mac.sh: unknown platform $RID" >&2; exit 1 ;;
esac

DMG="vlc-${VLC_VERSION}-${ARCH}.dmg"
URL="https://download.videolan.org/pub/videolan/vlc/${VLC_VERSION}/macosx/${DMG}"
mkdir -p "$CACHE"

if [ ! -f "$CACHE/$DMG" ]; then
  echo "== Downloading VLC ${VLC_VERSION} (${ARCH}) for video playback..."
  curl -Lsf --retry 3 -o "$CACHE/$DMG.part" "$URL"
  expected="$(curl -Lsf --retry 3 "$URL.sha256" | cut -d' ' -f1)"
  actual="$(shasum -a 256 "$CACHE/$DMG.part" | cut -d' ' -f1)"
  if [ -z "$expected" ] || [ "$expected" != "$actual" ]; then
    echo "!! The VLC download doesn't match VideoLAN's checksum — not using it." >&2
    rm -f "$CACHE/$DMG.part"
    exit 1
  fi
  mv "$CACHE/$DMG.part" "$CACHE/$DMG"
fi

# Open the disk image without showing it in Finder, copy what we need, close it again.
MOUNT="$(mktemp -d)"
hdiutil attach -nobrowse -readonly -mountpoint "$MOUNT" "$CACHE/$DMG" >/dev/null
trap 'hdiutil detach "$MOUNT" -quiet >/dev/null 2>&1 || true; rmdir "$MOUNT" 2>/dev/null || true' EXIT

SRC="$(find "$MOUNT" -maxdepth 2 -name 'VLC.app' -type d | head -1)/Contents/MacOS"
if [ ! -d "$SRC/lib" ]; then
  echo "!! Couldn't find VLC.app inside $DMG" >&2
  exit 1
fi

OUT="$APP/Contents/MacOS/libvlc/$RID/lib"
rm -rf "$APP/Contents/MacOS/libvlc/$RID"
mkdir -p "$OUT/vlc/share"
cp "$SRC"/lib/libvlc.[0-9]*.dylib "$OUT/libvlc.dylib"
cp "$SRC"/lib/libvlccore.[0-9]*.dylib "$OUT/libvlccore.dylib"
cp -R "$SRC/plugins" "$OUT/vlc/plugins"
[ -d "$SRC/share/hrtfs" ] && cp -R "$SRC/share/hrtfs" "$OUT/vlc/share/"

# The plugin list cache would still name the plugins removed below.
rm -f "$OUT/vlc/plugins/plugins.dat"

# Left out: GPL plugins (same list as VideoLAN's LGPL package), plugins that only
# work inside VLC.app, and ones a presentation app never uses (scripting, visualisers).
remove=(
  libdvdnav_plugin libdvdread_plugin
  libmad_plugin libmono_plugin libsamplerate_plugin
  libaribsub_plugin libfaad_plugin libt140_plugin libx264_plugin libx26410b_plugin libx265_plugin
  libdummy_plugin libgestures_plugin libhotkeys_plugin libmotion_plugin libnetsync_plugin liboldrc_plugin
  libmpc_plugin libreal_plugin libsid_plugin
  libmacosx_plugin libncurses_plugin
  libfile_logger_plugin
  libaudioscrobbler_plugin libexport_plugin liblogger_plugin libstats_plugin libvod_rtsp_plugin
  libpacketizer_a52_plugin
  libmediadirs_plugin libpodcast_plugin libsap_plugin
  libstream_out_cycle_plugin libstream_out_rtp_plugin
  libpostproc_plugin librotate_plugin
  libosx_notifications_plugin
  liblua_plugin libgoom_plugin libvisual_plugin libprojectm_plugin
)
for plugin in "${remove[@]}"; do
  rm -f "$OUT/vlc/plugins/$plugin.dylib"
done

echo "== VLC ${VLC_VERSION} added: $(find "$OUT/vlc/plugins" -name '*.dylib' | wc -l | tr -d ' ') plugins, $(du -sh "$OUT" | cut -f1)"
