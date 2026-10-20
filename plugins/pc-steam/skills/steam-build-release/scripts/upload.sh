#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: upload.sh <path/to/app_build_<appid>.vdf> [content_dir]" >&2
  echo "Env: STEAM_USERNAME (required), STEAMCMD, STEAM_PASSWORD, STEAM_GUARD_CODE, STEAM_CONFIG_VDF_BASE64, STEAM_CONFIG_DIR" >&2
  exit 64
}

[ "$#" -ge 1 ] || usage
app_vdf="$1"
content_dir="${2:-}"

: "${STEAM_USERNAME:?STEAM_USERNAME is not set}"
steamcmd="${STEAMCMD:-steamcmd}"

if [ ! -f "$app_vdf" ]; then
  echo "App build script not found: $app_vdf" >&2
  exit 66
fi
app_vdf="$(cd "$(dirname "$app_vdf")" && pwd)/$(basename "$app_vdf")"

if ! command -v "$steamcmd" >/dev/null 2>&1 && [ ! -x "$steamcmd" ]; then
  echo "steamcmd not found. Set STEAMCMD to its full path." >&2
  exit 69
fi

if [ -n "$content_dir" ]; then
  if [ ! -d "$content_dir" ]; then
    echo "Content folder not found: $content_dir" >&2
    exit 66
  fi
  leftovers="$(find "$content_dir" \( -name 'steam_appid.txt' -o -name '*_BurstDebugInformation_DoNotShip' -o -name '*_BackUpThisFolder_ButDontShipItWithYourGame' \) -print)"
  if [ -n "$leftovers" ]; then
    echo "Non-shipping files in content folder:" >&2
    echo "$leftovers" >&2
    exit 65
  fi
fi

if [ -n "${STEAM_CONFIG_VDF_BASE64:-}" ]; then
  config_dir="${STEAM_CONFIG_DIR:-$HOME/Steam/config}"
  mkdir -p "$config_dir"
  umask 077
  printf '%s' "$STEAM_CONFIG_VDF_BASE64" | base64 -d > "$config_dir/config.vdf"
fi

login=(+login "$STEAM_USERNAME")
if [ -n "${STEAM_PASSWORD:-}" ]; then
  login+=("$STEAM_PASSWORD")
fi

guard=()
if [ -n "${STEAM_GUARD_CODE:-}" ]; then
  guard=(+set_steam_guard_code "$STEAM_GUARD_CODE")
fi

"$steamcmd" +@ShutdownOnFailedCommand 1 +@NoPromptForPassword 1 ${guard[@]+"${guard[@]}"} "${login[@]}" +run_app_build "$app_vdf" +quit
