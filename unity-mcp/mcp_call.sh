#!/usr/bin/env bash
# Minimal MCP client helper for MCP for Unity (HTTP transport on 127.0.0.1:8080)
# Usage: bash mcp_call.sh <tool_name> '<json-args>'
set -euo pipefail
TOOL="$1"; ARGS="${2:-\{\}}"
PORT="${MCP_PORT:-8080}"

SID=$(curl -s -D - -o /dev/null --max-time 10 -X POST "http://127.0.0.1:${PORT}/mcp" \
  -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"freebuff-agent","version":"1.0"}}}' \
  | grep -i "^mcp-session-id:" | tr -d "\r" | awk '{print $2}')

curl -s --max-time 10 -X POST "http://127.0.0.1:${PORT}/mcp" \
  -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
  -H "Mcp-Session-Id: $SID" -d '{"jsonrpc":"2.0","method":"notifications/initialized"}' > /dev/null

# Extract the text payload from the SSE response
curl -s --max-time 120 -X POST "http://127.0.0.1:${PORT}/mcp" \
  -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
  -H "Mcp-Session-Id: $SID" \
  -d "$(printf '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"%s","arguments":%s}}' "$TOOL" "$ARGS")" \
  | grep "^data:" | sed 's/^data: //' | tail -1
