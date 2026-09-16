#!/usr/bin/env bash
# Runs the whole BoulderTime stack inside a GitHub Codespace (or any Linux box with Docker):
#   local Supabase (Postgres + Auth + Storage) → migrations + demo data → API → frontend.
# Usage:
#   bash scripts/dev.sh           start everything and print the link
#   bash scripts/dev.sh promote   after signing up: make every local user admin + owner of the demo gyms
#   bash scripts/dev.sh stop      stop everything
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"; cd "$ROOT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 ASPNETCORE_ENVIRONMENT=Development
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
LOGS="/tmp/bouldertime"; mkdir -p "$LOGS"
SUPA="npx -y supabase@2"
step() { printf "\n\033[1;38;5;208m▶ %s\033[0m\n" "$*"; }
fail() { printf "\n\033[1;31m✖ %s\033[0m\n" "$*"; exit 1; }

if [ "${1:-}" = "stop" ]; then
  pkill -f "BoulderTime.Api" 2>/dev/null; pkill -f "vite" 2>/dev/null
  $SUPA stop --workdir database >/dev/null 2>&1
  echo "Stopped."; exit 0
fi

if [ "${1:-}" = "promote" ]; then
  cd backend && dotnet run --project src/BoulderTime.Api -- dev-promote-all
  echo "Reload the app in your browser."; exit 0
fi

step "1/6 .NET 8"
if ! dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 >/dev/null || fail ".NET install failed"
  export DOTNET_ROOT="$HOME/.dotnet"
fi
dotnet tool install --global dotnet-ef --version 8.0.11 >/dev/null 2>&1 || true
echo "ok"

step "2/6 Local Supabase (first run downloads images: a few minutes)"
docker info >/dev/null 2>&1 || fail "Docker is not available in this environment."
if [ ! -f database/supabase/config.toml ]; then
  $SUPA init --workdir database --with-vscode-settings=false --with-intellij-settings=false >/dev/null 2>&1 \
    || (cd database && echo n | $SUPA init >/dev/null 2>&1) || fail "supabase init failed"
fi
# Skip services BoulderTime doesn't use to save memory; fall back to a full start if the CLI rejects a name.
$SUPA start --workdir database -x studio,imgproxy,edge-runtime,logflare,vector,realtime,postgres-meta > "$LOGS/supabase.log" 2>&1 \
  || $SUPA start --workdir database >> "$LOGS/supabase.log" 2>&1 \
  || { tail -20 "$LOGS/supabase.log"; fail "Supabase did not start"; }
STATUS="$($SUPA status --workdir database -o env 2>/dev/null)"
val() { echo "$STATUS" | grep -E "^$1=" | head -1 | cut -d= -f2- | tr -d '"'; }
ANON="$(val PUBLISHABLE_KEY)"; [ -z "$ANON" ] && ANON="$(val ANON_KEY)"
JWT_SECRET="$(val JWT_SECRET)"
[ -n "$ANON" ] || fail "Could not read the Supabase anon key (see: $SUPA status --workdir database)"
echo "ok"

step "3/6 Database migrations + demo gyms"
export ConnectionStrings__Database="Host=127.0.0.1;Port=54322;Database=postgres;Username=postgres;Password=postgres"
export Supabase__Url="http://127.0.0.1:54321" Supabase__JwtSecret="$JWT_SECRET"
(cd backend && dotnet run --project src/BoulderTime.Api -- migrate > "$LOGS/migrate.log" 2>&1) || { tail -30 "$LOGS/migrate.log"; fail "Migrations failed"; }
(cd backend && dotnet run --project src/BoulderTime.Api -- seed >> "$LOGS/migrate.log" 2>&1) || { tail -30 "$LOGS/migrate.log"; fail "Seeding failed"; }
echo "ok"

step "4/6 API"
pkill -f "BoulderTime.Api" 2>/dev/null; sleep 1
(cd backend && ASPNETCORE_URLS=http://127.0.0.1:5080 nohup dotnet run --project src/BoulderTime.Api --no-launch-profile > "$LOGS/api.log" 2>&1 &)
for i in $(seq 1 60); do curl -sf http://127.0.0.1:5080/api/health >/dev/null && break; sleep 2; done
curl -sf http://127.0.0.1:5080/api/health >/dev/null || { tail -30 "$LOGS/api.log"; fail "API did not start"; }
echo "ok"

step "5/6 Frontend"
cat > frontend/.env.local << ENV
VITE_SUPABASE_URL=/supabase
VITE_SUPABASE_ANON_KEY=$ANON
VITE_API_BASE_URL=/
ENV
(cd frontend && [ -d node_modules ] || npm ci --silent) >/dev/null 2>&1
pkill -f "vite" 2>/dev/null; sleep 1
(cd frontend && nohup npm run dev > "$LOGS/web.log" 2>&1 &)
for i in $(seq 1 40); do curl -sf http://127.0.0.1:5173 >/dev/null && break; sleep 1; done
curl -sf http://127.0.0.1:5173 >/dev/null || { tail -20 "$LOGS/web.log"; fail "Frontend did not start"; }
echo "ok"

step "6/6 Ready"
if [ -n "${CODESPACE_NAME:-}" ]; then
  URL="https://${CODESPACE_NAME}-5173.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}"
else
  URL="http://localhost:5173"
fi
printf "\n  Open BoulderTime:  \033[1;4m%s\033[0m\n\n" "$URL"
echo "  1. Create an account (email + any password, no confirmation needed locally)."
echo "  2. To unlock staff and admin areas:  bash scripts/dev.sh promote"
echo "  Logs: $LOGS   ·   Stop: bash scripts/dev.sh stop"
