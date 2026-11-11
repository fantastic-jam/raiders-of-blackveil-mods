#!/usr/bin/env bash
# Push a mod's Nexus page description and summary.
#   usage: nexus-update-description.sh <mod_dir>      e.g. mods/JoinAnytime
#   env:   NEXUS_API_KEY, NEXUS_API (default https://api.nexusmods.com/v3)
# Sources: <mod_dir>/nexus-description.txt (BBCode) → description,
#          <mod_dir>/metadata.json .nexus_short_description → summary (left untouched when absent).
# Skips (notice, exit 0) when the mod has no description file or no nexus_mod_uid.
# A failed update is a warning, not an error: the page just keeps its previous text.
set -euo pipefail

MOD_DIR="${1:?usage: $0 <mod_dir>}"
NEXUS_API="${NEXUS_API:-https://api.nexusmods.com/v3}"
DESC_FILE="$MOD_DIR/nexus-description.txt"
META_FILE="$MOD_DIR/metadata.json"
MOD_NAME="$(basename "$MOD_DIR")"

if [ ! -f "$DESC_FILE" ]; then
  echo "::notice::$MOD_NAME: no nexus-description.txt — mod page description left unchanged"
  exit 0
fi
MOD_UID=$( [ -f "$META_FILE" ] && jq -r '.nexus_mod_uid // empty' "$META_FILE" || true )
if [ -z "$MOD_UID" ]; then
  echo "::notice::$MOD_NAME: nexus_mod_uid not set — not published on Nexus, skipped"
  exit 0
fi

SUMMARY=$(jq -r '.nexus_short_description // empty' "$META_FILE")
BODY=$(jq -n \
  --rawfile description "$DESC_FILE" \
  --arg summary "$SUMMARY" \
  '{description: $description} + (if $summary == "" then {} else {summary: $summary} end)')

RESPONSE=$(mktemp)
STATUS=$(curl -s -o "$RESPONSE" -w "%{http_code}" -X PATCH \
  -H "apikey: $NEXUS_API_KEY" -H "Content-Type: application/json" \
  -d "$BODY" "$NEXUS_API/mods/$MOD_UID")
if [ "${STATUS:0:1}" != "2" ]; then
  echo "::warning::$MOD_NAME: mod page description update failed (HTTP $STATUS): $(cat "$RESPONSE")"
else
  echo "$MOD_NAME: mod page description updated (HTTP $STATUS)."
fi
