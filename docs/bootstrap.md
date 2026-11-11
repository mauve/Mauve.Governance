# Enroll or update a repository

The [Mauve.Governance Policy](governance.md) is authoritative in this
repository. Consumer-facing policy, skills, agents and
workflows installed by the tool originate from embedded `src/templates/`
assets, including `.editorconfig`, `AGENTS.md`, `governance.md` and
`style-guide.md`. These are **managed copies: do not edit
them directly**. Propose
changes to their templates and the original policy source here; add
consumer-specific rules in separate unmanaged files. The Governance root
itself is enrolled as a consumer: its `governance.md`, `style-guide.md` and
`docs/governance.md` are generated from templates. Its root README and
feature documents remain authored in place. The [profile reference](profiles.md) specifies
what each supported profile does and does not enforce.

The Tool also creates starter `docs/Requirements/README.md`,
`docs/Requirements/Planned/README.md` and `docs/validation-gaps.md`
from templates when missing. Unlike managed files, these are editable
repository documents: the Tool never takes ownership of or overwrites
existing content. The extension drafting guide at `docs/Policies/README.md`
is managed; write individual `POL-*.md` extensions beside it.

| Authored template | Installed here | Ownership |
| --- | --- | --- |
| `src/templates/governance.md` | `governance.md` | Managed consumer policy. |
| `src/templates/docs/governance.md` | `docs/governance.md` | Managed copy of the canonical policy; edit the template, not this copy. |
| `src/templates/style-guide.md` | `style-guide.md` | Managed style guide. |
| `src/templates/docs/Policies/README.md` | `docs/Policies/README.md` | Managed drafting guide; matches its template. |
| `src/templates/docs/Requirements/README.md` | `docs/Requirements/README.md` | Editable starter, expanded here for this repository. |
| `src/templates/docs/Requirements/Planned/README.md` | `docs/Requirements/Planned/README.md` | Editable starter, expanded here for this repository. |
| `src/templates/docs/validation-gaps.md` | `docs/validation-gaps.md` | Editable starter with this repository's requirement rows. |

## Local-first bootstrap and consumer verification

No public Governance NuGet release or hosted consumer run is currently
verified. For local verification, pack the Tool and its three Governance
dependencies from this checkout:

```sh
mkdir -p artifacts/local-feed
dotnet pack src/Mauve.Governance.TestResults -c Release -o artifacts/local-feed
dotnet pack src/Mauve.Governance.Traceability -c Release -o artifacts/local-feed
dotnet pack src/Mauve.Governance.Requirements -c Release -o artifacts/local-feed
dotnet pack src/Mauve.Governance.Testing -c Release -o artifacts/local-feed
dotnet pack src/Mauve.Governance.Testing.MSTest -c Release -o artifacts/local-feed
dotnet pack src/Mauve.Governance.Testing.Analyzers -c Release -o artifacts/local-feed
dotnet pack src/Mauve.Governance.Logging.Analyzers -c Release -o artifacts/local-feed
dotnet pack src/Mauve.Governance.Testing.TrxLogger -c Release -o artifacts/local-feed
dotnet pack src/Mauve.Governance.Tool -c Release -o artifacts/local-feed
```

Configure NuGet so Governance packages can only resolve from that feed and
third-party dependencies resolve from NuGet.org. NuGet may use cached
packages without applying source mapping, so also point `NUGET_PACKAGES` (or
`globalPackagesFolder`) at a fresh consumer-local directory:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="governance-local" value="/absolute/path/to/artifacts/local-feed" />
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
```

Use the exact version from `Directory.Build.props` (currently `0.13.0`):

```sh
export NUGET_PACKAGES="$PWD/artifacts/local-governance-cache-$$"
mkdir "$NUGET_PACKAGES"
dotnet tool install Mauve.Governance.Tool --version 0.13.0 \
  --tool-path "$HOME/.dotnet/tools/governance-local" \
  --configfile /path/to/NuGet.Config
"$HOME/.dotnet/tools/governance-local/governance" init \
  --root /path/to/your/repository --profile core --bundle-version 0.13.0
"$HOME/.dotnet/tools/governance-local/governance" validate \
  --root /path/to/your/repository
```

After `init` has installed the consumer's pinned tool manifest, verify that
it restores from the same feed/cache mapping:

```sh
cd /path/to/your/repository
dotnet tool restore --configfile /path/to/NuGet.Config
dotnet tool run governance -- validate --root .
```

Select `core-requirements` only when the consumer owns
`docs/Requirements/README.md` and `docs/Requirements/Planned/README.md`;
create these files and the `Planned/` directory before `init`. Review the
generated ownership manifest and diff, and keep editable feature documents
and `docs/validation-gaps.md` outside Governance ownership.

Run the end-to-end synthetic local package consumer from this repository:

```sh
bash tests/verify-local-consumer.sh
```

It packs locally, installs the exact Tool from an isolated local feed and
cache, creates fresh `core` and `core-requirements` MSTest consumers, runs
the packaged analyzer/adapter/TRX logger, imports real TRX, generates
source-only traceability, checks update no-ops, checks editable-file
preservation, and checks negative metadata and logging diagnostics.
Third-party packages still come from NuGet.org. This synthetic fixture is
not acceptance evidence from a product consumer or a hosted run.

The current Tool, MSTest metadata adapter and TRX logger target .NET 10;
first consumers using those packages must target `net10.0` and have the
.NET 10 SDK/runtime. The analyzers target `netstandard2.0`, but no earlier
consumer framework is validated by this end-to-end check. For the current
repository workflow use the pinned .NET 10 SDK `10.0.404`. Add
`Mauve.Governance.Testing.Analyzers` and the framework adapter to the test
project; add `Mauve.Governance.Logging.Analyzers` where logging analysis is
wanted. The `.editorconfig` only configures diagnostics: the consumer must
require its own project build to enforce them.

The package-reference pattern used by the local MSTest consumer is:

```xml
<ItemGroup>
  <PackageReference Include="MSTest" Version="4.5.1" />
  <PackageReference Include="Mauve.Governance.Testing.MSTest" Version="0.13.0" />
  <PackageReference Include="Mauve.Governance.Testing.Analyzers" Version="0.13.0" />
  <PackageReference Include="Mauve.Governance.Logging.Analyzers" Version="0.13.0" />
  <PackageReference Include="Mauve.Governance.Testing.TrxLogger" Version="0.13.0" />
</ItemGroup>
```

After annotating and running a test, execute the runner logger and evidence
import in the consumer:

```sh
dotnet test tests/Your.Tests.csproj --logger governance-trx \
  --results-directory TestResults
"$HOME/.dotnet/tools/governance-local/governance" test-results \
  --trx TestResults/governance-<run-id>.trx \
  --output artifacts/test-evidence.json
"$HOME/.dotnet/tools/governance-local/governance" generate --root . \
  --output artifacts/traceability.json
```

TRX evidence describes observed outcomes. Generated traceability is
source-only and is not execution evidence. Verify the actual chosen
consumer's framework, analyzers, restore sources, diagnostics and required
gates separately before claiming product readiness.

## Published/hosted bootstrap (not yet verified)

Once a trusted package release and approved feed are provisioned, install
the exact published `Mauve.Governance.Tool` version and use the same
`init --check`, review and `validate` sequence described above. Hosted
NuGet-dependent policy checks are disabled unless the Actions variable
`GOVERNANCE_HOSTED_ENABLED` is exactly `true`. Update PR automation is
disabled unless `GOVERNANCE_RELEASE_UPDATES_ENABLED` is exactly `true`;
neither opt-in makes an unpublished package or unverified App operational.
Do not configure or advertise
scheduled update PR automation until the release workflow, GitHub App
permissions and a hosted update run have been verified.

## Existing Governance installation

Use a trusted newer tool to run `governance update --root <repository>
--check`, then `--apply` in a review branch. The known 0.2.0-0.12.0
ownership manifests migrate to the current bundle; edited *owned* files
are replaced in the proposed diff, while any file absent from the old
manifest remains outside Governance ownership. An unknown schema, an
unsupported version, unexpected owned hashes or an unmanaged path
collision blocks migration. Move or reconcile such files explicitly
before trying again; do not delete consumer material automatically.

An ungoverned repository has no update workflow or ownership manifest.
No central release can reach it by itself: use the local `init` path above.
`update --check` and `update --apply` reconcile an installed version and
change local files only; they never open a PR. Release discovery and
automated update PRs are deferred pending a verified published feed and
authorized hosted execution. The former `.mauve-governance/update.sh` is
retired: migrations
remove it only when a previous manifest records its ownership.

## Feature lifecycle and scope

Keep active features under `docs/Requirements/` and proposed work under
`docs/Requirements/Planned/`, following the
[feature lifecycle](Requirements/README.md). Feature documents are
consumer-authored, never overwritten by template reconciliation. The
reusable `Mauve.Governance.Requirements` library validates the document
structure used by the Tool; a Markdown heading inside a code block is
not a policy section. .NET repositories should adopt Central Package
Management and committed lock files. The current `core` gate checks
managed-file integrity, present feature-document structure and
active-requirement/source/ledger declarations; it is **not** the full
policy or observed evidence gate. For run
evidence use metadata-bearing TRX and `governance test-results`, not the
source-only traceability index.

The copied `.editorconfig` sets the logging and test-metadata analyzer
diagnostics to errors, but the `core` workflow does not install the analyzer
package or build projects. .NET consumers that need enforcement must install
`Mauve.Governance.Logging.Analyzers` in their projects and require a build
in CI. The distributed style guide is advisory; it does not add a formatting
gate.

Publishing the tool package and release assets, hosting the GitHub App,
and protecting the consumer's required checks are external prerequisites;
the repository currently has no verified public release or hosted PR run.
