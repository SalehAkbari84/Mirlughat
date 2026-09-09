#!/usr/bin/env bash
# Safety preflight for MCP for Unity (read-only, makes zero changes).
# Run this BEFORE letting the agent use any MCP tool against Unity.

PORT="${1:-8080}"
EXPECTED_PROJECT="${2:-TopDownCarGame}"
URL="http://127.0.0.1:${PORT}/mcp"

echo "== 1. Port ${PORT} reachable? =="
if ! curl -s -o /dev/null -w "%{http_code}" --max-time 4 "${URL}" | grep -qE "400|401|404|405|200"; then
  echo "   FAIL: no MCP server on ${URL} (is Unity open with the MCP for Unity server running?)"
  exit 1
fi
echo "   OK: server answers"

echo "== 2. Loopback-only binding? =="
BINDS=$(netstat -ano -p tcp | grep LISTENING | grep -E "0\.0\.0\.0:${PORT}|\[::\]:${PORT}")
if [ -n "$BINDS" ]; then
  echo "   WARNING: something on ${PORT} is exposed beyond loopback:"
  echo "$BINDS"
  echo "   Do NOT connect until MCP for Unity binds 127.0.0.1 only."
  exit 1
fi
echo "   OK: loopback only"

echo "== 3. Connected Unity project (expect: ${EXPECTED_PROJECT}) =="
echo "   -> In the Unity MCP window, 'Session Active (...)' must say: ${EXPECTED_PROJECT}"
echo "   -> Usage: ./check-connection.sh [port] [expected-project]  e.g. ./check-connection.sh 8080 Mirlughat"

echo "Preflight passed. Safe to connect."
