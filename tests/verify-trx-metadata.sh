#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
results="$root/artifacts/trx-metadata-check"
mkdir -p "$results"

for framework in MSTest XunitV2 NUnit; do
    project="tests/Mauve.Governance.Testing.$framework.Tests"
    output="$results/$framework"
    mkdir -p "$output"
    rm -f "$output"/governance-*.trx
    dotnet test "$project" --logger governance-trx --results-directory "$output" -v:q
done

v3="tests/Mauve.Governance.Testing.XunitV3.Tests"
dotnet build "$v3" -v:q
mkdir -p "$results/XunitV3"
rm -f "$results/XunitV3"/governance-*.trx
dotnet vstest "$v3/bin/Debug/net10.0/Mauve.Governance.Testing.XunitV3.Tests.dll" \
    /Logger:governance-trx "/ResultsDirectory:$results/XunitV3"

files=()
for framework in MSTest XunitV2 NUnit XunitV3; do
    mapfile -t emitted < <(find "$results/$framework" -maxdepth 1 -name 'governance-*.trx' -type f)
    if (( ${#emitted[@]} != 1 )); then
        echo "Expected one governance TRX for $framework, found ${#emitted[@]}" >&2
        exit 1
    fi
    files+=("${emitted[0]}")
done

dotnet run --project tests/Mauve.Governance.TrxVerification -- "${files[@]}"
trx_args=()
for file in "${files[@]}"; do
    trx_args+=(--trx "$file")
done
dotnet run --project src/Mauve.Governance.Tool -c Release --no-build -- \
    test-results "${trx_args[@]}" --output "$results/evidence.json"
