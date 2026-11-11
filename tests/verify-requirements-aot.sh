#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
fixture="$(mktemp -d)"
restore_locks() {
    dotnet restore "$root/Mauve.Governance.slnx" --force-evaluate --verbosity quiet
    rm -r "$fixture"
}
trap restore_locks EXIT
project="$root/tests/Mauve.Governance.Requirements.AotSmoke/Mauve.Governance.Requirements.AotSmoke.csproj"
dotnet restore "$project" -r linux-x64 -p:PublishAot=true --force-evaluate --verbosity quiet
dotnet publish "$project" -c Release -r linux-x64 -p:PublishAot=true \
    --no-restore -warnaserror --verbosity quiet -o "$fixture"
"$fixture/Mauve.Governance.Requirements.AotSmoke"
