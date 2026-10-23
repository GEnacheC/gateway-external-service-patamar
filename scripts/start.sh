#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

echo
echo "========================================"
echo "  Patamar Gateway - inicializacao"
echo "========================================"
echo

if ! command -v docker >/dev/null 2>&1; then
  echo "[ERRO] Docker nao encontrado."
  echo
  echo "Instale o Docker Desktop:"
  echo "  https://www.docker.com/products/docker-desktop/"
  echo "Depois reinicie o computador e execute este script de novo."
  echo
  echo "macOS (Homebrew): brew install --cask docker"
  echo "Linux: siga a documentacao oficial do Docker Engine/Compose."
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "[ERRO] Docker instalado, mas nao esta em execucao."
  echo
  echo "Abra o Docker Desktop (ou o servico docker) e espere ficar pronto."
  echo "Depois execute este script de novo."
  exit 1
fi

if [[ ! -f .env ]]; then
  if [[ -f .env.example ]]; then
    cp .env.example .env
    echo "[OK] Arquivo .env criado a partir de .env.example"
  else
    echo "[AVISO] .env.example nao encontrado. Continuando sem .env..."
  fi
else
  echo "[OK] Arquivo .env ja existe"
fi

echo
echo "Baixando imagens e subindo a API + banco..."
echo "Isso pode demorar alguns minutos na primeira vez."
echo

docker compose up --build -d

echo
echo "Aguardando a API ficar pronta..."

ready=0
for _ in $(seq 1 60); do
  if curl -sf http://localhost:8080/health >/dev/null 2>&1; then
    ready=1
    break
  fi
  sleep 2
done

if [[ "$ready" -eq 1 ]]; then
  echo "[OK] API respondendo em http://localhost:8080/health"
else
  echo
  echo "[AVISO] Containers subiram, mas a API ainda nao respondeu em /health."
  echo "Aguarde mais alguns segundos e abra: http://localhost:8080/health"
fi

echo
echo "========================================"
echo "  Pronto!"
echo "========================================"
echo
echo "  API:     http://localhost:8080"
echo "  Saude:   http://localhost:8080/health"
echo
echo "  Para parar tudo depois, rode: ./scripts/stop.sh"
echo
echo "  Exemplo Curitiba (sync):"
echo "  POST http://localhost:8080/api/events/sync?lat=-25.4284&long=-49.2733&provider=sympla-public"
echo
echo "  Exemplo Curitiba (listar):"
echo "  GET  http://localhost:8080/api/events?lat=-25.4284&long=-49.2733"
echo
