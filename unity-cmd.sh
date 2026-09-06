#!/usr/bin/env bash
# Synchronous Unity Bridge command wrapper (macOS / Linux).
# Mirrors unity-cmd.ps1: writes a JSON request, polls the response file
# for an mtime change, prints the response, exits 0; non-zero on timeout.
#
# Usage:
#   ./unity-cmd.sh '{"type":"help"}'
#   ./unity-cmd.sh '{"type":"refresh"}' -t 180
#   ./unity-cmd.sh -f request.json
#   ./unity-cmd.sh -f complex-batch.json -t 120

set -euo pipefail

CMD=""
FILE=""
TIMEOUT=60

usage() {
  cat <<EOF
Usage: unity-cmd.sh <json-command> [-t <seconds>]
       unity-cmd.sh -f <path.json> [-t <seconds>]

Examples:
  ./unity-cmd.sh '{"type":"help"}'
  ./unity-cmd.sh -f request.json
  ./unity-cmd.sh -f complex-batch.json -t 120
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -f|--file)
      FILE="${2:-}"; shift 2 ;;
    -t|--timeout)
      TIMEOUT="${2:-}"; shift 2 ;;
    -h|--help)
      usage; exit 0 ;;
    -*)
      echo "Unknown option: $1" >&2; usage; exit 1 ;;
    *)
      if [[ -n "$CMD" ]]; then
        echo "Multiple command strings given." >&2; exit 1
      fi
      CMD="$1"; shift ;;
  esac
done

if [[ -n "$FILE" && -n "$CMD" ]]; then
  echo "Specify either -f/--file or a command string, not both." >&2
  exit 1
fi

if [[ -n "$FILE" ]]; then
  if [[ ! -f "$FILE" ]]; then
    echo "File not found: $FILE" >&2; exit 1
  fi
  CMD="$(cat "$FILE")"
  if [[ -z "${CMD//[[:space:]]/}" ]]; then
    echo "File is empty: $FILE" >&2; exit 1
  fi
elif [[ -z "$CMD" ]]; then
  usage; exit 1
fi

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BRIDGE_DIR="$PROJECT_ROOT/Assets/LLM/Bridge"
REQUEST_FILE="$BRIDGE_DIR/request.json"
RESPONSE_FILE="$BRIDGE_DIR/response.md"

mkdir -p "$BRIDGE_DIR"

# Portable mtime in epoch seconds: BSD stat (macOS) first, GNU stat (Linux) fallback.
mtime() {
  if [[ ! -e "$1" ]]; then echo 0; return; fi
  stat -f %m "$1" 2>/dev/null || stat -c %Y "$1"
}

BEFORE="$(mtime "$RESPONSE_FILE")"

# Write request without trailing newline (mirrors the PS1 behaviour).
printf '%s' "$CMD" > "$REQUEST_FILE"

POLL_INTERVAL=0.5
ELAPSED=0

# Use awk for float arithmetic — bash can't do floats natively.
while awk "BEGIN{exit !($ELAPSED < $TIMEOUT)}"; do
  sleep "$POLL_INTERVAL"
  ELAPSED="$(awk "BEGIN{print $ELAPSED + $POLL_INTERVAL}")"

  if [[ -f "$RESPONSE_FILE" ]]; then
    CURRENT="$(mtime "$RESPONSE_FILE")"
    if [[ "$CURRENT" -gt "$BEFORE" ]]; then
      cat "$RESPONSE_FILE"
      exit 0
    fi
  fi
done

echo "Timeout (${TIMEOUT}s) waiting for Unity response. Is Unity running?" >&2
exit 1
