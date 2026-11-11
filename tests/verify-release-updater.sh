#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
fixture="$(mktemp -d)"
trap 'rm -r "$fixture"' EXIT
mkdir -p "$fixture/bin" "$fixture/consumer/.config" "$fixture/consumer/.mauve-governance"
cat > "$fixture/bin/gh" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
if [[ "$1 $2" != 'release view' ]]; then
    echo "Unexpected gh invocation: $*" >&2
    exit 1
fi
printf '%s\n' "${MOCK_RELEASE_TAG:?}"
MOCK
chmod +x "$fixture/bin/gh"
export PATH="$fixture/bin:$PATH" GITHUB_OUTPUT="$fixture/outputs"
tool="$root/src/Mauve.Governance.Tool/bin/Release/net10.0/Mauve.Governance.Tool.dll"
test -f "$tool"
printf '%s\n' '{"tools":{"mauve.governance.tool":{"version":"0.12.0"}}}' \
    > "$fixture/consumer/.config/dotnet-tools.json"
printf '%s\n' '{"BundleVersion":"0.12.0"}' \
    > "$fixture/consumer/.mauve-governance/manifest.json"

export MOCK_RELEASE_TAG=v0.12.0
dotnet "$tool" update-release --root "$fixture/consumer"
grep -qx 'changed=false' "$GITHUB_OUTPUT"
test "$(wc -l < "$GITHUB_OUTPUT")" -eq 1

export MOCK_RELEASE_TAG=v0.11.0
if dotnet "$tool" update-release --root "$fixture/consumer" > "$fixture/downgrade.log" 2>&1; then
    echo 'Downgrade release was accepted.' >&2
    exit 1
fi
grep -q 'MG-GOV-0002: Release .* would downgrade' "$fixture/downgrade.log"
test "$(wc -l < "$GITHUB_OUTPUT")" -eq 1

export MOCK_RELEASE_TAG=invalid
if dotnet "$tool" update-release --root "$fixture/consumer" > "$fixture/invalid.log" 2>&1; then
    echo 'Malformed release tag was accepted.' >&2
    exit 1
fi
grep -q 'MG-GOV-0002: Latest Governance release has an unsupported tag' "$fixture/invalid.log"
test "$(wc -l < "$GITHUB_OUTPUT")" -eq 1
echo 'Verified Tool-owned updater no-op, downgrade and malformed-tag rejection.'
