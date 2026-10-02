#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

echo
echo "========================================"
echo "  Patamar Gateway - encerramento"
echo "========================================"
echo

if ! command -v docker >/dev/null 2>&1; then
  echo "[ERRO] Docker nao encontrado."
  exit 1
fi

docker compose down

echo
echo "[OK] API e banco parados."
echo
