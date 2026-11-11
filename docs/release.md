# Package and consumer release

`Directory.Build.props` gives the single .NET tool, reusable libraries,
analyzers and framework adapters one version. The manual
[`release.yml`](../.github/workflows/release.yml) workflow runs from
`main`, performs locked restore/build and the local test/evidence checks,
packs the solution, then publishes NuGet packages and a matching GitHub
release containing their `.nupkg` assets. The release upload supplies
GitHub SHA-256 asset digests consumed by each installed update workflow.
The job that runs source code has read-only GitHub permissions; NuGet
credentials and a write token are available only to the subsequent
publishing job. Publishing requires `GOVERNANCE_NUGET_API_KEY` as a repository
secret and an appropriately protected default branch.

The pinned local Tool's `update-release --root <repository>` command reads
releases from `mauve/Mauve.Governance`. It verifies the SHA-256 digests of
the tool and its required libraries before installing the exact released
Tool from an isolated local feed. NuGet package-source mapping restricts
`Mauve.Governance.*` to that feed; third-party transitive packages use
NuGet.org. On a newer compatible release the Tool reconciles only
manifest-owned assets and missing editable scaffolds, validates and stages
their changes. Existing editable files are never replaced. The installed
workflow separately opens or refreshes a reviewable PR using a
short-lived GitHub App token; no write token is used during Tool
execution. Consumers must provision
`GOVERNANCE_APP_ID` (Actions variable) and `GOVERNANCE_APP_PRIVATE_KEY`
(Actions secret) on an App with Contents and Pull requests write
permissions. With no enrolled workflow, a repository requires explicit
local `init` before any automatic updates are possible.
The required consumer declaration gate now validates the editable
per-requirement ledger and source links. The merged manual/automated
release gap report remains a **planned, report-only** evidence layer:
this release workflow does not yet produce that report, import manual
runs or block a release on missing run evidence.

**Current local evidence:** the 0.12.0 locked build, package pack and
root/example template reconciliation pass. Earlier 0.11.0 isolated-feed
Tool installation and `core-requirements` validation passed. The reusable Markdown validator
passes a warning-free .NET 10 NativeAOT publish and execution. Tool-owned
updater fixtures cover no-op, downgrade, malformed releases, digest
tampering, ownership validation, retired-script staging and unmanaged
path rejection. Local Git fixtures cover PR creation, refresh and refusal
of unmanaged PR-branch edits; metadata-bearing real TRX pass/fail/skip
runs cover all four test frameworks. These are local checks, not evidence
of a published release. There is no accessible public
`mauve/Mauve.Governance` release repository at present. Do not claim that publishing, hosted
update PRs, signed package provenance, or branch-protected checks have
run. Configure the repository/feed and observe a hosted release plus
consumer PR before treating automatic rollout as operational.
