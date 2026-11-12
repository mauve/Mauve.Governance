#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
results="$root/artifacts/trx-outcome-check"
mkdir -p "$results"
files=()
v3='tests/Mauve.Governance.Testing.XunitV3.Tests'
dotnet build "$v3" -p:DefineConstants=GOVERNANCE_OUTCOME_FIXTURE -v:q

for framework in MSTest XunitV2 XunitV3 NUnit; do
    for run in 1 2; do
        output="$results/$framework/run-$run"
        mkdir -p "$output"
        rm -f "$output"/governance-*.trx
        if [[ "$framework" == XunitV3 ]]; then
            if dotnet vstest "$v3/bin/Debug/net10.0/Mauve.Governance.Testing.XunitV3.Tests.dll" \
                /Logger:governance-trx '/Logger:trx;LogFileName=official.trx' "/ResultsDirectory:$output"; then
                echo "The intentional $framework failure fixture unexpectedly passed." >&2
                exit 1
            fi
        else
            if [[ "$framework" == MSTest ]]; then
                project='tests/Mauve.Governance.LoggerOutcomes.Tests'
                arguments=()
            else
                project="tests/Mauve.Governance.Testing.$framework.Tests"
                arguments=(-p:DefineConstants=GOVERNANCE_OUTCOME_FIXTURE)
            fi
            if dotnet test "$project" "${arguments[@]}" --logger governance-trx \
                --logger 'trx;LogFileName=official.trx' \
                --results-directory "$output" -v:q; then
                echo "The intentional $framework failure fixture unexpectedly passed." >&2
                exit 1
            fi
        fi
        mapfile -t emitted < <(find "$output" -maxdepth 1 -name 'governance-*.trx' -type f)
        if (( ${#emitted[@]} != 1 )); then
            echo "Expected one metadata TRX for $framework run $run, got ${#emitted[@]}." >&2
            exit 1
        fi
        files+=("${emitted[0]}")
    done
done

dotnet run --project tests/Mauve.Governance.TrxVerification -- --diagnostics "${files[@]}"

trx_args=()
for file in "${files[@]}"; do
    trx_args+=(--trx "$file")
done
dotnet run --project src/Mauve.Governance.Tool -c Release --no-build -- \
    test-results "${trx_args[@]}" --output "$results/evidence.json"
jq -e '
    .Runs | length == 8
' "$results/evidence.json" >/dev/null
jq -e '
    .Features | any(.Id == "FEAT-03" and .Status == "Mixed"
        and .Passed == 8 and .Failed == 8 and .Skipped == 8)
' "$results/evidence.json" >/dev/null
jq -e '
    .Runs | all(.Tests | map(select(.Feature == "FEAT-03")) |
        length == 3 and any(.Outcome == "Passed")
        and any(.Outcome == "Failed") and any(.Outcome == "Skipped"))
' "$results/evidence.json" >/dev/null
echo 'Verified real parameterized pass/fail, skip and distinct rerun evidence for all four frameworks.'
