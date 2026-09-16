set -u
cd "$(git rev-parse --show-toplevel)"
LOG="$PWD/verify-log.txt"; : > "$LOG"
say() { echo "$*" | tee -a "$LOG"; }
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
STATUS=ok

if ! dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  say "== Installo .NET 8 =="
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 >/dev/null
  export DOTNET_ROOT="$HOME/.dotnet"
fi
say "SDK: $(dotnet --version)"
cd backend

say "== BUILD =="
if dotnet build > /tmp/build.log 2>&1; then say OK
else grep -E "error|warning" /tmp/build.log | sort -u | head -40 | tee -a "$LOG"; STATUS=fail; fi

if [ "$STATUS" = ok ]; then
  say "== MIGRAZIONE =="
  dotnet tool install --global dotnet-ef --version 8.0.11 >/dev/null 2>&1 || true
  if dotnet ef migrations add InitialCreate --project src/BoulderTime.Infrastructure --startup-project src/BoulderTime.Api --output-dir Persistence/Migrations > /tmp/ef.log 2>&1; then say OK
  else tail -30 /tmp/ef.log | tee -a "$LOG"; STATUS=fail; fi
fi

if [ "$STATUS" = ok ]; then
  say "== TEST =="
  if docker info >/dev/null 2>&1; then
    dotnet test > /tmp/test.log 2>&1 && say OK || STATUS=fail
    grep -E "Passed!|Failed!|\[FAIL\]|error" /tmp/test.log | head -40 | tee -a "$LOG"
  else say "Docker non disponibile: test saltati"; fi
fi

cd ..
say "== ESITO: $STATUS =="
rm -f verify.sh
git add -A
git commit -qm "Phase 1 verification: $STATUS" && git push -q origin main
echo "FINITO - esito: $STATUS"
