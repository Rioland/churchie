#!/usr/bin/env bash
# Builds the Windows installer (Churchie-Setup-win-x64.exe) with NSIS.
#   usage: make-windows-installer.sh <folder-with-Churchie.exe> <version> <output.exe>
#
# NSIS runs on Windows, Linux (apt install nsis) and macOS (brew install makensis).
# GitHub's Windows machines already have it.
set -euo pipefail

SRC="$(cd "${1:?folder with Churchie.exe}" && pwd)"
VERSION="${2:?version, e.g. 0.1.0}"
OUTFILE="${3:?output .exe path}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

[ -f "$SRC/Churchie.exe" ] || { echo "No Churchie.exe in $SRC" >&2; exit 1; }

# Find makensis: on the PATH, or where the Windows installer of NSIS puts it.
MAKENSIS="$(command -v makensis || true)"
for guess in "/c/Program Files (x86)/NSIS/makensis.exe" "/c/Program Files/NSIS/makensis.exe"; do
  [ -z "$MAKENSIS" ] && [ -x "$guess" ] && MAKENSIS="$guess"
done
[ -n "$MAKENSIS" ] || { echo "makensis not found: install NSIS (https://nsis.sourceforge.io)" >&2; exit 1; }

# The version in Windows' file details must look like 1.2.3 (digits only).
NUMERIC_VERSION="$(echo "$VERSION" | sed -E 's/[^0-9.].*$//')"
case "$NUMERIC_VERSION" in *.*.*) ;; *) NUMERIC_VERSION="0.1.0" ;; esac

# The uninstaller removes exactly the files we install, so list them now
# (files first, then folders from the deepest up; RMDir only removes empty folders).
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
UNLIST="$WORK/uninstall-list.nsh"
(
  cd "$SRC"
  find . -type f | sed 's|^\./||' | sort | while read -r f; do
    printf 'Delete "$INSTDIR\\%s"\n' "${f//\//\\}"
  done
  find . -mindepth 1 -type d | sed 's|^\./||' | sort -r | while read -r d; do
    printf 'RMDir "$INSTDIR\\%s"\n' "${d//\//\\}"
  done
) > "$UNLIST"

mkdir -p "$(dirname "$OUTFILE")"
OUTFILE="$(cd "$(dirname "$OUTFILE")" && pwd)/$(basename "$OUTFILE")"
ICON="$ROOT/src/Churchie.App/Assets/churchie.ico"
NSI="$ROOT/build/windows/churchie.nsi"

# On Windows (Git Bash), makensis.exe needs C:\style paths.
winpath() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else echo "$1"; fi; }

echo "== Building the Windows installer ($VERSION)..."
"$MAKENSIS" -V2 \
  "-DVERSION=$NUMERIC_VERSION" \
  "-DSRC=$(winpath "$SRC")" \
  "-DOUTFILE=$(winpath "$OUTFILE")" \
  "-DICON=$(winpath "$ICON")" \
  "-DART=$(winpath "$ROOT/build/windows")" \
  "-DUNLIST=$(winpath "$UNLIST")" \
  "$(winpath "$NSI")"
echo "Created $OUTFILE"
