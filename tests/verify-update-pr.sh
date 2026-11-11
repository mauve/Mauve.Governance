#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
fixture="$(mktemp -d)"
trap 'rm -r "$fixture"' EXIT
git init --bare -q "$fixture/origin.git"
git init -q -b main "$fixture/consumer"
cd "$fixture/consumer"
git config user.name 'Governance fixture'
git config user.email 'fixture@example.invalid'
mkdir -p .mauve-governance docs/governance "$fixture/bin"
printf '%s\n' '{"BundleVersion":"0.10.0","Profile":"core","Files":[{"Path":"docs/governance/README.md"},{"Path":".mauve-governance/update.sh"}]}' > .mauve-governance/manifest.json
printf '%s\n' 'old owned updater' > .mauve-governance/update.sh
printf '%s\n' 'old managed text' > docs/governance/README.md
git add .
git commit -qm 'Initial consumer'
git remote add origin "$fixture/origin.git"
git push -q -u origin main

cat > "$fixture/bin/gh" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
case "$1 $2" in
    'auth setup-git') ;;
    'pr list') printf '%s\n' "${MOCK_PR_NUMBER:-}" ;;
    'pr create'|'pr edit')
        printf '%s\n' "$1 $2" >> "$MOCK_PR_LOG"
        while (( $# )); do
            if [[ "$1" == '--body' ]]; then
                printf '%s\n' "$2" > "$MOCK_PR_BODY"
                break
            fi
            shift
        done
        ;;
    *) echo "Unexpected gh invocation: $*" >&2; exit 1 ;;
esac
MOCK
chmod +x "$fixture/bin/gh"
export PATH="$fixture/bin:$PATH" GH_TOKEN=fixture VERSION=0.11.0 BASE=main
export MOCK_PR_LOG="$fixture/pr.log" MOCK_PR_BODY="$fixture/pr-body.txt"

propose() {
    awk '/^        run: \|$/ { block = ""; capture = 1; next }
         capture { sub(/^          /, ""); block = block $0 "\n" }
         END { printf "%s", block }' "$root/.github/workflows/governance-update.yml" | bash
}

printf '%s\n' '{"BundleVersion":"0.11.0","Profile":"core","Files":[{"Path":"docs/governance/README.md"}]}' > .mauve-governance/manifest.json
git rm -q .mauve-governance/update.sh
printf '%s\n' 'new managed text' > docs/governance/README.md
git add .
propose
test "$(git --git-dir="$fixture/origin.git" rev-parse refs/heads/governance/managed-update)" = \
    "$(git rev-parse HEAD)"
grep -q '^pr create$' "$MOCK_PR_LOG"
grep -q "Previous bundle: \`0.10.0\`" "$MOCK_PR_BODY"
grep -q "New bundle: \`0.11.0\`" "$MOCK_PR_BODY"
grep -q '.mauve-governance/update.sh' "$MOCK_PR_BODY"
grep -q "Selected profile: \`core\`" "$MOCK_PR_BODY"
grep -q 'docs/governance/README.md' "$MOCK_PR_BODY"
grep -q 'Manual obligations:' "$MOCK_PR_BODY"

git switch -q main
git branch -D governance/managed-update > /dev/null
printf '%s\n' 'refreshed managed text' > docs/governance/README.md
git add .
export MOCK_PR_NUMBER=42
propose
test "$(git --git-dir="$fixture/origin.git" rev-parse refs/heads/governance/managed-update)" = \
    "$(git rev-parse HEAD)"
grep -q '^pr edit$' "$MOCK_PR_LOG"
grep -q 'refreshed managed text' docs/governance/README.md

printf '%s\n' 'unmanaged change' > consumer-owned.txt
git add consumer-owned.txt
git commit -qm 'Unmanaged branch edit'
git push -q origin HEAD:refs/heads/governance/managed-update
remote="$(git --git-dir="$fixture/origin.git" rev-parse refs/heads/governance/managed-update)"
git switch -q main
git branch -D governance/managed-update > /dev/null
printf '%s\n' 'further managed text' > docs/governance/README.md
git add .
if propose > "$fixture/rejection.log" 2>&1; then
    echo 'An unmanaged PR-branch change was accepted.' >&2
    exit 1
fi
grep -q 'Update branch contains an unmanaged change: consumer-owned.txt' "$fixture/rejection.log"
test "$(git --git-dir="$fixture/origin.git" rev-parse refs/heads/governance/managed-update)" = "$remote"
test "$(wc -l < "$MOCK_PR_LOG")" -eq 2
echo 'Verified update PR creation, refresh and unmanaged-branch rejection.'
