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
# Downloads are kept in ~/Library/Caches/Churchie/vlc, shared by every copy of the
# project on this Mac (the test build and the download packages use different
# folders), so each VLC file is only downloaded once.
set -euo pipefail

APP="${1:?usage: get-vlc-mac.sh <Churchie.app> <rid>}"
RID="${2:?usage: get-vlc-mac.sh <Churchie.app> <rid>}"
VLC_VERSION="3.0.24"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOCAL_CACHE="$ROOT/.cache/vlc"   # where earlier versions of this script kept the downloads
SHARED_CACHE="${CHURCHIE_CACHE:-$HOME/Library/Caches/Churchie}/vlc"
if mkdir -p "$SHARED_CACHE" 2>/dev/null && [ -w "$SHARED_CACHE" ]; then
  CACHE="$SHARED_CACHE"
else
  CACHE="$LOCAL_CACHE"           # fall back to this project's own folder
fi

case "$RID" in
  osx-arm64) ARCH=arm64 ;;
  osx-x64)   ARCH=intel64 ;;
  *) echo "get-vlc-mac.sh: unknown platform $RID" >&2; exit 1 ;;
esac

DMG="vlc-${VLC_VERSION}-${ARCH}.dmg"
FILE="vlc/${VLC_VERSION}/macosx/${DMG}"
OFFICIAL="https://download.videolan.org/pub/videolan"
# The download comes from VideoLAN's own address (it picks a mirror near you) or from a
# few large mirrors. A slow or broken connection is expected: whatever has arrived is
# kept in a ".part" file and the next try carries on from there (-C -). A source that
# stalls (under 5 KB/s for a minute) is left for the next one. Whichever mirror the file
# comes from, it is checked against VideoLAN's own checksum before it is used.
MIRRORS=(
  "$OFFICIAL"
  "https://mirror.init7.net/videolan"
  "https://ftp.fau.de/videolan"
  "https://ftp.osuosl.org/pub/videolan"
)
mkdir -p "$CACHE"

# Already downloaded by an earlier build into this project's own folder? Share that copy.
if [ ! -f "$CACHE/$DMG" ] && [ -f "$LOCAL_CACHE/$DMG" ]; then
  cp "$LOCAL_CACHE/$DMG" "$CACHE/$DMG"
fi

if [ ! -f "$CACHE/$DMG" ]; then
  expected="$(curl -Lsf --retry 3 "$OFFICIAL/$FILE.sha256" | cut -d' ' -f1 || true)"
  if [ -z "$expected" ]; then
    echo "!! Couldn't get VideoLAN's checksum for $DMG (is the Mac online?)." >&2
    exit 1
  fi
  part_is_good() {
    [ -f "$CACHE/$DMG.part" ] && [ "$(shasum -a 256 "$CACHE/$DMG.part" | cut -d' ' -f1)" = "$expected" ]
  }

  finished=""
  if part_is_good; then finished=yes; fi
  if [ -z "$finished" ]; then
    echo "== Downloading VLC ${VLC_VERSION} (${ARCH}) for video playback (about 60 MB, once only)."
    echo "   If this is stopped, running it again carries on from where it stopped."
  fi
  # Up to 3 rounds over all the sources; each try picks up where the last one stopped.
  for round in 1 2 3; do
    [ -n "$finished" ] && break
    for mirror in "${MIRRORS[@]}"; do
      if curl -Lf -# --retry 3 -C - --speed-limit 5000 --speed-time 60 \
           -o "$CACHE/$DMG.part" "$mirror/$FILE"; then
        finished=yes
        break
      fi
      echo "   (the download stopped - trying again from another mirror)"
    done
  done

  if [ -z "$finished" ]; then
    echo "!! The VLC download didn't finish (slow or no internet). Run this again to carry on." >&2
    exit 1
  fi
  if ! part_is_good; then
    echo "!! The VLC download doesn't match VideoLAN's checksum - not using it." >&2
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
