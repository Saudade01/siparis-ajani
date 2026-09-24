#!/usr/bin/env bash
# CI'daki restore/format/build/test adımlarının yerel karşılığı.
# Herhangi bir adım hata verirse betik orada durur.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SOLUTION="$ROOT_DIR/SiparisAjani.slnx"

if command -v dotnet >/dev/null 2>&1; then
  DOTNET_BIN="dotnet"
elif [ -x "$HOME/.dotnet/dotnet" ]; then
  DOTNET_BIN="$HOME/.dotnet/dotnet"
else
  echo "Hata: dotnet bulunamadı (PATH ya da \$HOME/.dotnet)." >&2
  exit 1
fi

echo "==> Bağımlılıklar geri yükleniyor"
"$DOTNET_BIN" restore "$SOLUTION"

echo "==> Biçim denetleniyor"
"$DOTNET_BIN" format "$SOLUTION" --verify-no-changes --no-restore

echo "==> Derleniyor (Release)"
"$DOTNET_BIN" build "$SOLUTION" -c Release --no-restore

echo "==> Testler çalıştırılıyor"
"$DOTNET_BIN" test "$SOLUTION" -c Release --no-build --logger "trx" --results-directory "$ROOT_DIR/TestResults"

echo "Tümü geçti"
