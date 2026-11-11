#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

for framework in MSTest XunitV2 NUnit; do
    requirement=FEAT-Payments-04
    if [ "$framework" = MSTest ]; then
        requirement=FEAT-Payments-02
    fi
    output=$(dotnet test "tests/Mauve.Governance.Testing.$framework.Tests" \
        --no-restore --verbosity quiet --filter "Requirement=$requirement")
    printf '%s\n' "$output"
    grep -Eq 'Passed:[[:space:]]+1,[[:space:]]+Skipped:[[:space:]]+0,[[:space:]]+Total:[[:space:]]+1,' <<< "$output"
done

output=$(dotnet run --no-restore --project tests/Mauve.Governance.Testing.XunitV3.Tests \
    -- -trait 'Requirement=FEAT-Payments-04' -noLogo)
printf '%s\n' "$output"
grep -Eq 'Total:[[:space:]]+1,[[:space:]]+Errors:[[:space:]]+0,[[:space:]]+Failed:[[:space:]]+0,' <<< "$output"
