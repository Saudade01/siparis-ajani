#!/usr/bin/env bash
# API (:5080) ve Web (:5090) sunucularını birlikte başlatır.
# --with-react verilirse ve clients/web-react mevcutsa React istemcisi de başlatılır.
# Ctrl+C hepsini kapatır.
set -euo pipefail

WITH_REACT=0
for arg in "$@"; do
  case "$arg" in
    --with-react)
      WITH_REACT=1
      ;;
    *)
      echo "Bilinmeyen seçenek: $arg" >&2
      exit 1
      ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
REACT_DIR="$ROOT_DIR/clients/web-react"

API_URL="http://localhost:5080"
WEB_URL="http://localhost:5090"
HEALTH_URL="$API_URL/api/health"

if command -v dotnet >/dev/null 2>&1; then
  DOTNET_BIN="dotnet"
elif [ -x "$HOME/.dotnet/dotnet" ]; then
  DOTNET_BIN="$HOME/.dotnet/dotnet"
else
  echo "Hata: dotnet bulunamadı (PATH ya da \$HOME/.dotnet)." >&2
  exit 1
fi

REACT_ENABLED=0
if [ "$WITH_REACT" -eq 1 ]; then
  if [ -f "$REACT_DIR/package.json" ]; then
    if command -v npm >/dev/null 2>&1; then
      REACT_ENABLED=1
    else
      echo "Uyarı: --with-react verildi ama npm bulunamadı, React istemcisi atlanıyor." >&2
    fi
  else
    echo "Uyarı: --with-react verildi ama clients/web-react bulunamadı, atlanıyor." >&2
  fi
fi

if [ "$REACT_ENABLED" -eq 1 ]; then
  echo "API: $API_URL · Arayüz: $WEB_URL · React: clients/web-react"
else
  echo "API: $API_URL · Arayüz: $WEB_URL"
fi

API_PID=""
WEB_PID=""
REACT_PID=""

cleanup() {
  echo ""
  echo "Kapatılıyor..."
  if [ -n "${REACT_PID:-}" ]; then
    kill "$REACT_PID" 2>/dev/null || true
  fi
  if [ -n "${WEB_PID:-}" ]; then
    kill "$WEB_PID" 2>/dev/null || true
  fi
  if [ -n "${API_PID:-}" ]; then
    kill "$API_PID" 2>/dev/null || true
  fi
  wait 2>/dev/null || true
  exit 0
}
trap cleanup INT TERM

"$DOTNET_BIN" run --project "$ROOT_DIR/src/SiparisAjani.Api" --launch-profile http &
API_PID=$!

echo "API'nin hazır olması bekleniyor ($HEALTH_URL)..."
ready=0
for _ in $(seq 1 60); do
  if curl -fsS "$HEALTH_URL" >/dev/null 2>&1; then
    ready=1
    break
  fi
  sleep 1
done

if [ "$ready" -eq 1 ]; then
  echo "API hazır."
else
  echo "Uyarı: API 60 sn içinde /api/health yanıtı vermedi, yine de devam ediliyor." >&2
fi

"$DOTNET_BIN" run --project "$ROOT_DIR/src/SiparisAjani.Web" --launch-profile http &
WEB_PID=$!

if [ "$REACT_ENABLED" -eq 1 ]; then
  (cd "$REACT_DIR" && npm run dev) &
  REACT_PID=$!
fi

wait
