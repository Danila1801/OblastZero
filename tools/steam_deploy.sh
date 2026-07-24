# Steam Deploy Script (SteamCMD)
# Run after building the game with Unity. Uploads to your configured depot.
#
# Prerequisites:
#   1. SteamCMD installed: https://partner.steamgames.com/doc/sdk/uploading
#   2. Steam account with Publisher permissions on the app
#   3. Depot config files created once via Steamworks Admin → SteamPipe tab
#   4. APP_ID set (real one or 480 for test)
#
# Usage:
#   ./steam_deploy.sh /path/to/build/folder "v0.1.0-beta"
#
# Windows users: run via Git Bash, or port to `.bat` / `steamcmd.exe +login...`.

set -euo pipefail

BUILD_DIR="${1:?Usage: $0 <build_dir> <version_label>}"
VERSION="${2:?Usage: $0 <build_dir> <version_label>}"
APP_ID="${STEAM_APP_ID:-480}"
DEPOT_ID="${STEAM_DEPOT_ID:-481}"  # Change to your real depot
DESCRIPTION="Oblast Zero ${VERSION}"

# Locate SteamCMD
if command -v steamcmd >/dev/null 2>&1; then
    STEAMCMD=steamcmd
elif [ -x "$HOME/steamcmd/steamcmd.sh" ]; then
    STEAMCMD="$HOME/steamcmd/steamcmd.sh"
else
    echo "❌ SteamCMD not found. Install from https://partner.steamgames.com/doc/sdk/uploading"
    exit 1
fi

# Sanity checks
if [ ! -d "$BUILD_DIR" ]; then
    echo "❌ Build directory not found: $BUILD_DIR"
    exit 1
fi

# Check credentials (set as env vars or prompted)
STEAM_USER="${STEAM_USER:-}"
STEAM_PASS="${STEAM_PASS:-}"
if [ -z "$STEAM_USER" ]; then
    read -p "Steam username: " STEAM_USER
fi
if [ -z "$STEAM_PASS" ]; then
    read -s -p "Steam password: " STEAM_PASS
    echo
fi

cat <<EOF
======================================
OBLAST ZERO — Steam Deploy
======================================
App ID:    $APP_ID
Depot ID:  $DEPOT_ID
Build:     $BUILD_DIR
Version:   $VERSION
User:      $STEAM_USER
======================================
EOF

# Build the app build script inline (normally you'd have depot_build.vdf committed)
DEPOT_BUILD_VDF="/tmp/oblastzero_depot_${VERSION}.vdf"
cat > "$DEPOT_BUILD_VDF" <<VDF
"DepotBuildConfig"
{
    "DepotID" "$DEPOT_ID"
    "contentroot" "$BUILD_DIR"
    "FileMapping"
    {
        "LocalPath" "*"
        "DepotPath" "."
        "recursive" "1"
    }
    "FileExclusion" "*.pdb"
    "FileExclusion" "*.meta"
    "FileExclusion" "*.tmp"
}
VDF

APP_BUILD_VDF="/tmp/oblastzero_app_${VERSION}.vdf"
cat > "$APP_BUILD_VDF" <<VDF
"appbuild"
{
    "appid" "$APP_ID"
    "desc" "$DESCRIPTION"
    "buildoutput" "/tmp/steam_build_output/"
    "contentroot" "$BUILD_DIR"
    "depots"
    {
        "$DEPOT_ID" "$DEPOT_BUILD_VDF"
    }
}
VDF

mkdir -p /tmp/steam_build_output/

echo "🚀 Running SteamCMD..."
$STEAMCMD +login "$STEAM_USER" "$STEAM_PASS" +run_app_build "$APP_BUILD_VDF" +quit

echo "✅ Upload complete. Check https://partner.steamgames.com/apps/$APP_ID/builds for status."
