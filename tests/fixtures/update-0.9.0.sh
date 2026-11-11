#!/usr/bin/env bash
set -euo pipefail

source_repository='mauve/Mauve.Governance'
tag=$(gh release view --repo "$source_repository" --json tagName --jq '.tagName')
if [[ ! "$tag" =~ ^v([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
  echo "MG-GOV-0002: Latest Governance release has an unsupported tag: $tag" >&2
  exit 1
fi
version="${BASH_REMATCH[1]}"
installed=$(jq -er '.tools["mauve.governance.tool"].version' .config/dotnet-tools.json)
if [[ "$version" == "$installed" ]]; then
  echo 'changed=false' >> "$GITHUB_OUTPUT"
  exit 0
fi
if [[ "$(printf '%s\n%s\n' "$version" "$installed" | sort -V | head -n 1)" != "$installed" ]]; then
  echo "MG-GOV-0002: Release $version would downgrade installed bundle $installed." >&2
  exit 1
fi

work=$(mktemp -d "${RUNNER_TEMP}/governance-update.XXXXXXXX")
feed="$work/feed"
mkdir "$feed"
gh api "repos/$source_repository/releases/tags/$tag" > "$work/release.json"
for package in "Mauve.Governance.Tool.$version.nupkg" "Mauve.Governance.TestResults.$version.nupkg" "Mauve.Governance.Traceability.$version.nupkg"; do
  digest=$(jq -er --arg package "$package" '.assets[] | select(.name == $package) | .digest' "$work/release.json")
  if [[ ! "$digest" =~ ^sha256:([[:xdigit:]]{64})$ ]]; then
    echo "MG-GOV-0002: Missing trusted SHA-256 digest for $package." >&2
    exit 1
  fi
  gh release download "$tag" --repo "$source_repository" --pattern "$package" --dir "$feed"
  actual=$(sha256sum "$feed/$package")
  actual="${actual%% *}"
  if [[ "${actual,,}" != "${BASH_REMATCH[1],,}" ]]; then
    echo "MG-GOV-0002: Release asset digest mismatch for $package." >&2
    exit 1
  fi
done

cat > "$work/NuGet.Config" <<EOF
<configuration>
  <packageSources>
    <clear/>
    <add key="governance-release" value="$feed" />
    <add key="nuget-org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="governance-release"><package pattern="Mauve.Governance.*" /></packageSource>
    <packageSource key="nuget-org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
EOF
NUGET_PACKAGES="$work/packages" dotnet tool install Mauve.Governance.Tool --version "$version" --tool-path "$work/tool" --configfile "$work/NuGet.Config"
"$work/tool/governance" update --root . --apply
"$work/tool/governance" validate --root .

git add -- .mauve-governance/manifest.json
while IFS= read -r path; do
  git add -A -- "$path"
done < <(jq -er '.Files[].Path' .mauve-governance/manifest.json)
if git diff --cached --quiet; then
  echo 'changed=false' >> "$GITHUB_OUTPUT"
else
  echo "version=$version" >> "$GITHUB_OUTPUT"
  echo 'changed=true' >> "$GITHUB_OUTPUT"
fi
