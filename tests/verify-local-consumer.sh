#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

for command in dotnet jq sha256sum; do
    if ! command -v "$command" >/dev/null 2>&1; then
        echo "Required command not found: $command" >&2
        exit 1
    fi
done

version="$(dotnet msbuild src/Mauve.Governance.Tool/Mauve.Governance.Tool.csproj \
    -getProperty:Version -nologo)"
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Could not determine the current Governance package version: $version" >&2
    exit 1
fi

work="$(mktemp -d)"
trap 'rm -rf -- "$work"' EXIT
cp "$root/global.json" "$work/global.json"

feed="$work/feed"
mkdir -p "$feed"

packages=(
    src/Mauve.Governance.Tool
    src/Mauve.Governance.TestResults
    src/Mauve.Governance.Traceability
    src/Mauve.Governance.Requirements
    src/Mauve.Governance.Testing
    src/Mauve.Governance.Testing.MSTest
    src/Mauve.Governance.Testing.Analyzers
    src/Mauve.Governance.Logging.Analyzers
    src/Mauve.Governance.Testing.TrxLogger
)
for project in "${packages[@]}"; do
    dotnet pack "$project" --configuration Release --output "$feed" --verbosity quiet
done

cat > "$work/NuGet.Config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="governance-local" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="governance-local">
      <package pattern="Mauve.Governance.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

export NUGET_PACKAGES="$work/packages"
export NUGET_HTTP_CACHE_PATH="$work/http-cache"
export DOTNET_CLI_HOME="$work/cli-home"

tool="$work/tool/governance"
dotnet tool install Mauve.Governance.Tool --version "$version" \
    --tool-path "$work/tool" --configfile "$work/NuGet.Config" --verbosity quiet

seed_consumer() {
    local profile="$1"
    local consumer="$work/$profile"
    mkdir -p "$consumer/docs/Requirements/Planned" "$consumer/src"
    cat > "$consumer/docs/Requirements/README.md" <<'EOF'
# Requirement lifecycle

Features are documented here and proposals belong in `Planned/`.
EOF
    cat > "$consumer/docs/Requirements/Planned/README.md" <<'EOF'
# Planned requirements

Draft requirements are not active implementation or test evidence.
EOF
    cat > "$consumer/docs/Requirements/FEAT-Consumer-Smoke.md" <<'EOF'
# FEAT-Consumer: Local package consumer

## Status

**PARTIAL**. This synthetic consumer exercises local package installation.

## Capability

Verify the packages through a newly created MSTest project.

## Context

The fixture uses the packaged metadata analyzer, MSTest adapter and TRX logger.

## Requirements

### FEAT-Consumer-001 - Packaged metadata reaches the runner

The test declares its owning feature and requirement, and the runner TRX
preserves that metadata for evidence import.

**Acceptance:** a real MSTest run is imported with a passing observation.

**Security:** missing test metadata is rejected by an analyzer diagnostic.

## Metrics Impact

No runtime metrics are introduced by this consumer verification.

## Verification

`tests/verify-local-consumer.sh` creates and validates this disposable consumer.

## Diagrams

The package-to-test-runner path is linear; no placement diagram is needed.
EOF
    cat > "$consumer/docs/validation-gaps.md" <<'EOF'
# Validation gaps

| Requirement | Validation method | Evidence or procedure | Owner | Environment / prerequisites | Gap or waiver details |
| --- | --- | --- | --- | --- | --- |
| FEAT-Consumer-001 | portable automated | `src/ConsumerTests.cs` | Consumer smoke test | .NET 10 SDK | Executed locally by the consumer readiness script. |
EOF
    cat > "$consumer/src/ConsumerTests.cs" <<'EOF'
using Mauve.Governance.Testing.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LocalConsumer;

[TestClass]
public sealed class ConsumerTests
{
    [TestMethod]
    [TestProperty(Metadata.Feature, "FEAT-Consumer")]
    [TestProperty(Metadata.Requirement, "FEAT-Consumer-001")]
    public void PackagedTestMetadataIsEmitted() =>
        Assert.IsNotNull(typeof(ConsumerTests).GetMethod(nameof(PackagedTestMetadataIsEmitted)));
}
EOF
    cat > "$consumer/Consumer.Tests.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GovernanceVersion>$version</GovernanceVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest" Version="4.5.1" />
    <PackageReference Include="Mauve.Governance.Testing.MSTest" Version="\$(GovernanceVersion)" />
    <PackageReference Include="Mauve.Governance.Testing.Analyzers" Version="\$(GovernanceVersion)" />
    <PackageReference Include="Mauve.Governance.Logging.Analyzers" Version="\$(GovernanceVersion)" />
    <PackageReference Include="Mauve.Governance.Testing.TrxLogger" Version="\$(GovernanceVersion)" />
  </ItemGroup>
</Project>
EOF
}

for profile in core core-requirements; do
    consumer="$work/$profile"
    seed_consumer "$profile"

    if "$tool" init --root "$consumer" --profile "$profile" \
        --bundle-version "$version" --check; then
        echo "Expected the first $profile init --check to report pending files." >&2
        exit 1
    else
        status=$?
        if (( status != 1 )); then
            echo "Unexpected init --check exit code for $profile: $status" >&2
            exit "$status"
        fi
    fi

    "$tool" init --root "$consumer" --profile "$profile" --bundle-version "$version"
    "$tool" validate --root "$consumer"

    project="$consumer/Consumer.Tests.csproj"
    (
        cd "$consumer"
        dotnet tool restore --configfile "$work/NuGet.Config" --verbosity quiet
        dotnet tool run governance -- validate --root .
        dotnet restore "$project" --configfile "$work/NuGet.Config" \
            --packages "$NUGET_PACKAGES" --verbosity quiet
        dotnet test "$project" --no-restore --logger governance-trx \
            --results-directory "$consumer/TestResults" --verbosity quiet
    )

    mapfile -t trx_files < <(find "$consumer/TestResults" -maxdepth 1 \
        -type f -name 'governance-*.trx' -print)
    if (( ${#trx_files[@]} != 1 )); then
        echo "Expected one metadata-bearing MSTest TRX for $profile; found ${#trx_files[@]}." >&2
        exit 1
    fi
    report="$consumer/test-evidence.json"
    "$tool" test-results --trx "${trx_files[0]}" --output "$report"
    jq -e '
        .Runs | length == 1
    ' "$report" >/dev/null
    jq -e '
        .Features | any(.Id == "FEAT-Consumer" and .Status == "Passed" and .Passed == 1)
    ' "$report" >/dev/null
    jq -e '
        .Requirements | any(.Id == "FEAT-Consumer-001" and .Status == "Passed" and .Passed == 1)
    ' "$report" >/dev/null

    traceability="$consumer/traceability.json"
    "$tool" generate --root "$consumer" --output "$traceability"
    jq -e '
        [.features[].id] | index("FEAT-Consumer") != null
    ' "$traceability" >/dev/null
    jq -e '
        [.tests[].requirements[]] | index("FEAT-Consumer-001") != null
    ' "$traceability" >/dev/null

    cat > "$consumer/docs/Requirements/Consumer-Editable-Notes.md" <<'EOF'
# Consumer-owned content

This file must survive a no-op Governance update.
EOF
    before="$(find "$consumer/docs/Requirements" -type f -print0 |
        sort -z | xargs -0 sha256sum; sha256sum "$consumer/docs/validation-gaps.md")"
    "$tool" update --root "$consumer" --check
    "$tool" update --root "$consumer" --apply
    after="$(find "$consumer/docs/Requirements" -type f -print0 |
        sort -z | xargs -0 sha256sum; sha256sum "$consumer/docs/validation-gaps.md")"
    if [[ "$before" != "$after" ]]; then
        echo "No-op $profile update modified consumer-owned requirement files." >&2
        diff -u <(printf '%s\n' "$before") <(printf '%s\n' "$after") || true
        exit 1
    fi

    echo "Verified $profile enrollment, validation, packaged MSTest/TRX evidence, generation and editable-file preservation."
done

negative="$work/negative"
mkdir -p "$negative/src"
cp "$work/core/.editorconfig" "$negative/.editorconfig"
cat > "$negative/Negative.Tests.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest" Version="4.5.1" />
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Mauve.Governance.Testing.Analyzers" Version="$version" />
    <PackageReference Include="Mauve.Governance.Logging.Analyzers" Version="$version" />
  </ItemGroup>
</Project>
EOF
cat > "$negative/src/NegativeTests.cs" <<'EOF'
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class NegativeTests
{
    [TestMethod]
    public void MissingMetadataIsRejected() =>
        Assert.IsNotNull(typeof(NegativeTests).GetMethod(nameof(MissingMetadataIsRejected)));

    public static void UnstructuredLogIsRejected(ILogger logger) =>
        logger.LogInformation($"Order received {Guid.NewGuid()}");
}
EOF
(
    cd "$negative"
    dotnet restore "$negative/Negative.Tests.csproj" --configfile "$work/NuGet.Config" \
        --packages "$NUGET_PACKAGES" --verbosity quiet
)
if output="$(cd "$negative" && dotnet build "$negative/Negative.Tests.csproj" --no-restore \
    -warnaserror:MGTEST001,MGLOG001,MGLOG002 --verbosity quiet 2>&1)"; then
    echo "Expected missing metadata and invalid logging diagnostics to fail the negative build." >&2
    exit 1
fi
printf '%s\n' "$output"
for diagnostic in MGTEST001 MGLOG001 MGLOG002; do
    if ! grep -q "$diagnostic" <<<"$output"; then
        echo "Expected negative smoke diagnostic not found: $diagnostic" >&2
        exit 1
    fi
done

echo "Verified negative analyzer enforcement for missing metadata and structured logging."
