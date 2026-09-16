#!/usr/bin/env bash
# Runs the whole BoulderTime stack for local development (GitHub Codespaces or any Linux box with Docker):
#   PostgreSQL + Supabase Auth (GoTrue) in Docker → migrations + demo data → API → frontend.
#
# This deliberately does not use the Supabase CLI: its full local stack fails to initialise inside Codespaces.
# GoTrue is the same auth server hosted Supabase runs, so tokens have the same shape as in production.
#
# Usage:
#   bash scripts/dev.sh           start everything and print the link
#   bash scripts/dev.sh promote   after signing up: make every local user admin + owner of the demo gyms
#   bash scripts/dev.sh stop      stop everything (data is kept)
#   bash scripts/dev.sh reset     stop and delete all local data
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"; cd "$ROOT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 ASPNETCORE_ENVIRONMENT=Development
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
LOGS="/tmp/bouldertime"; mkdir -p "$LOGS"

DB=bt-db; DB_IMAGE=postgres:16-alpine; DB_VOLUME=bt-db-data
AUTH=bt-auth; AUTH_IMAGE=public.ecr.aws/supabase/gotrue:v2.194.0
# Local-only secrets. Never reuse these anywhere else.
JWT_SECRET="local-dev-jwt-secret-bouldertime-at-least-32-chars"
ANON_KEY="local-dev-anon-key"
# Issuer the API expects: {Supabase:Url}/auth/v1. Nothing needs to listen on this port.
SUPABASE_URL="http://127.0.0.1:54321"

DB_PORT=54322; AUTH_PORT=9999
export ConnectionStrings__Database="Host=127.0.0.1;Port=54322;Database=postgres;Username=postgres;Password=postgres"
export Supabase__Url="$SUPABASE_URL" Supabase__JwtSecret="$JWT_SECRET"

step() { printf "\n\033[1;38;5;208m▶ %s\033[0m\n" "$*"; }

# On failure, publish diagnostics to the repository so they can be read without screenshots.
fail() {
  printf "\n\033[1;31m✖ %s\033[0m\n" "$*"
  {
    echo "== FAILED: $* =="; date -u
    for c in $DB $AUTH; do echo; echo "== docker logs $c =="; docker logs --tail 40 "$c" 2>&1; done
    for f in migrate api web; do [ -f "$LOGS/$f.log" ] && { echo; echo "== $f.log =="; tail -40 "$LOGS/$f.log"; }; done
    echo; echo "== probes =="
    (timeout 3 bash -c "</dev/tcp/127.0.0.1/$DB_PORT" && echo "db port $DB_PORT reachable") 2>&1 || echo "db port $DB_PORT NOT reachable"
    curl -s -m 3 "http://127.0.0.1:$AUTH_PORT/health" || echo "auth health NOT reachable"
    echo; echo "== containers =="; docker ps -a --format '{{.Names}}  {{.Status}}  {{.Image}}' 2>&1
    echo; echo "== resources =="; df -h / | tail -1; free -m | head -2
  } > "$ROOT/dev-log.txt" 2>&1
  if git add dev-log.txt >/dev/null 2>&1 && git commit -qm "dev.sh diagnostics" >/dev/null 2>&1 && git push -q origin HEAD >/dev/null 2>&1; then
    echo "Diagnostics published to dev-log.txt in the repository."
  fi
  exit 1
}

stop_all() { pkill -f "BoulderTime.Api" 2>/dev/null; pkill -f "vite" 2>/dev/null; docker stop $AUTH $DB >/dev/null 2>&1; }

case "${1:-}" in
  stop) stop_all; echo "Stopped. Data is kept."; exit 0 ;;
  reset) stop_all; docker rm -f $AUTH $DB >/dev/null 2>&1; docker volume rm $DB_VOLUME >/dev/null 2>&1; echo "Local data deleted."; exit 0 ;;
  promote) cd backend && dotnet run --project src/BoulderTime.Api -- dev-promote-all && echo "Reload the app in your browser."; exit 0 ;;
esac

# Containers are recreated on every start so configuration changes always apply.
# Postgres data lives in a named volume and survives this; Auth is stateless.
recreate() { local name=$1; shift; docker rm -f "$name" >/dev/null 2>&1; docker run -d --name "$name" "$@" >/dev/null; }

step "1/6 Tools"
command -v docker >/dev/null && docker info >/dev/null 2>&1 || fail "Docker is not available in this environment."
if ! dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 >/dev/null || fail ".NET install failed"
  export DOTNET_ROOT="$HOME/.dotnet"
fi
# Free disk taken by earlier Supabase CLI attempts (images not used by this stack).
docker images --format '{{.Repository}}:{{.Tag}}' 2>/dev/null | grep 'supabase/' | grep -v "$AUTH_IMAGE" | xargs -r docker rmi >/dev/null 2>&1
echo "ok"

step "2/6 Database + Auth (first run downloads two images)"
# Host networking: container-to-container bridge networking is unreliable inside Codespaces' Docker-in-Docker,
# so both services bind to the host's loopback, exactly like a native install.
docker network rm bouldertime-dev >/dev/null 2>&1
recreate $DB --network host -v $DB_VOLUME:/var/lib/postgresql/data -e POSTGRES_PASSWORD=postgres \
  $DB_IMAGE -c port=$DB_PORT -c listen_addresses=127.0.0.1 || fail "Could not start PostgreSQL"
for i in $(seq 1 60); do docker exec $DB pg_isready -h 127.0.0.1 -p $DB_PORT -U postgres >/dev/null 2>&1 && break; sleep 1; done
docker exec $DB pg_isready -h 127.0.0.1 -p $DB_PORT -U postgres >/dev/null 2>&1 || fail "PostgreSQL did not become ready"
docker exec -i $DB psql -h 127.0.0.1 -p $DB_PORT -v ON_ERROR_STOP=1 -q -U postgres -d postgres < database/dev/auth-init.sql > "$LOGS/auth-init.log" 2>&1 \
  || fail "Auth database setup failed"

recreate $AUTH --network host \
  -e GOTRUE_API_HOST=127.0.0.1 -e PORT=$AUTH_PORT -e GOTRUE_LOG_LEVEL=debug \
  -e API_EXTERNAL_URL="$SUPABASE_URL/auth/v1" -e GOTRUE_SITE_URL=http://localhost:5173 -e GOTRUE_URI_ALLOW_LIST='*' \
  -e GOTRUE_DB_DRIVER=postgres -e DATABASE_URL="postgres://supabase_auth_admin:postgres@127.0.0.1:$DB_PORT/postgres?sslmode=disable" \
  -e GOTRUE_JWT_SECRET="$JWT_SECRET" -e GOTRUE_JWT_ISSUER="$SUPABASE_URL/auth/v1" -e GOTRUE_JWT_AUD=authenticated \
  -e GOTRUE_JWT_EXP=3600 -e GOTRUE_JWT_DEFAULT_GROUP_NAME=authenticated -e GOTRUE_JWT_ADMIN_ROLES=service_role \
  -e GOTRUE_DISABLE_SIGNUP=false -e GOTRUE_EXTERNAL_EMAIL_ENABLED=true -e GOTRUE_MAILER_AUTOCONFIRM=true \
  $AUTH_IMAGE || fail "Could not start Supabase Auth"
for i in $(seq 1 120); do curl -sf "http://127.0.0.1:$AUTH_PORT/health" >/dev/null && break; sleep 1; done
curl -sf "http://127.0.0.1:$AUTH_PORT/health" >/dev/null || fail "Supabase Auth did not become healthy"
echo "ok"

step "3/6 Database migrations + demo gyms"
(cd backend && dotnet run --project src/BoulderTime.Api -- migrate > "$LOGS/migrate.log" 2>&1) || fail "Migrations failed"
(cd backend && dotnet run --project src/BoulderTime.Api -- seed >> "$LOGS/migrate.log" 2>&1) || fail "Seeding failed"
echo "ok"

step "4/6 API"
pkill -f "BoulderTime.Api" 2>/dev/null; sleep 1
(cd backend && ASPNETCORE_URLS=http://127.0.0.1:5080 setsid nohup dotnet run --project src/BoulderTime.Api --no-launch-profile > "$LOGS/api.log" 2>&1 < /dev/null &)
for i in $(seq 1 90); do curl -sf http://127.0.0.1:5080/api/health >/dev/null && break; sleep 2; done
curl -sf http://127.0.0.1:5080/api/health >/dev/null || fail "API did not start"
echo "ok"

step "5/6 Frontend"
printf 'VITE_SUPABASE_URL=/supabase\nVITE_SUPABASE_ANON_KEY=%s\nVITE_API_BASE_URL=/\n' "$ANON_KEY" > frontend/.env.local
if [ ! -d frontend/node_modules ]; then (cd frontend && npm ci --silent > "$LOGS/web.log" 2>&1) || fail "npm install failed"; fi
pkill -f "vite" 2>/dev/null; sleep 1
(cd frontend && setsid nohup npm run dev > "$LOGS/web.log" 2>&1 < /dev/null &)
for i in $(seq 1 60); do curl -sf http://127.0.0.1:5173 >/dev/null && break; sleep 1; done
curl -sf http://127.0.0.1:5173 >/dev/null || fail "Frontend did not start"
# End-to-end check: sign-up endpoint reachable through the same proxy the browser uses.
curl -sf http://127.0.0.1:5173/supabase/auth/v1/health >/dev/null || fail "Auth is not reachable through the frontend proxy"
echo "ok"

step "6/6 Ready"
if [ -n "${CODESPACE_NAME:-}" ]; then
  URL="https://${CODESPACE_NAME}-5173.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}"
else
  URL="http://localhost:5173"
fi
printf "\n  Open BoulderTime:  \033[1;4m%s\033[0m\n\n" "$URL"
echo "  1. Create an account (any email and an 8+ character password; no confirmation needed locally)."
echo "  2. To unlock staff and admin areas:  bash scripts/dev.sh promote"
echo "  Logs: $LOGS   ·   Stop: bash scripts/dev.sh stop"
