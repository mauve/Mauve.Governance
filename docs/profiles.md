# Governance profiles

The [Mauve.Governance Policy](governance.md) defines the framework
contract. A profile selects consumer checks and values; it does not turn a
planned policy rule into an enforced check. Fleet-specific paths, SDK choices,
diagnostic prefixes and performance thresholds are not defaults.

## Supported profiles: `core` and `core-requirements`

The 0.13.0 tool accepts `--profile core` or
`--profile core-requirements`, with `--bundle-version 0.13.0`, on `init`.
It writes the selected `Profile` and `"BundleVersion": "0.13.0"` to the
manifest at `.mauve-governance/manifest.json`, along with owned paths and
content hashes. `update` preserves the selected profile and migrates known
0.2.0–0.12.0 manifests (bundles before 0.9.0 were `core`). Unknown profiles,
manifest schemas, versions and
owned-file hashes are rejected rather than treated as compatible.

| Consumer operation | What both profiles currently enforce |
| --- | --- |
| `init` | Explicit enrollment; unmanaged path collisions block installation. |
| `update` | Known-version migration and managed-path reconciliation; unmanaged files are not adopted or overwritten. |
| `validate` | Manifest/managed-file integrity, active/planned feature-document structure **when `docs/Requirements/` exists**, and active-requirement/source/ledger declaration checks. |
| Installed CI workflow | Framework builds its Tool from source. Consumer hosted validation requires explicit `GOVERNANCE_HOSTED_ENABLED=true` and the pinned package on an approved feed; local-only consumers run `validate` and `generate` locally instead. Disabled hosted jobs are not passing evidence. |

The managed bundle copies a pinned .NET tool manifest, `.editorconfig`,
`AGENTS.md`, a style guide, two custom agents, three skills, a consumer
policy, the canonical policy and validation and update-PR workflows.
It also installs the managed `docs/Policies/README.md` drafting guide.
The editable `docs/Requirements/README.md`,
`docs/Requirements/Planned/README.md` and `docs/validation-gaps.md`
are copied from templates only when absent; they are not recorded in
the ownership manifest or overwritten after repository-specific edits.
The sole Tool embeds their authored sources from
`src/templates/`; the shell update script is retired. Managed Markdown
copies link to the canonical policy and warn against direct editing; editable
starters link to that policy but may be adapted. The ownership manifest
uses deterministic two-space-indented JSON.
The installed `governance.md` uses `MGOV-POL-xxx` section IDs and
`style-guide.md` uses `MSTY-POL-xxx`; the canonical framework policy
authored in `src/templates/docs/governance.md` and installed at
`docs/governance.md` retains its `MG-POL-xxx` IDs.
The [bootstrap guide](bootstrap.md) gives the exact enrollment, migration
and GitHub App setup steps.

`core-requirements` includes all `core` checks and additionally requires
`docs/Requirements/README.md` and
`docs/Requirements/Planned/README.md` to exist. These lifecycle files and
feature specifications are editable; the Tool preserves existing files
and does not overwrite them. Create the lifecycle layout before enrollment
with this profile; a missing
`docs/Requirements/` directory fails enrollment and validation under
this profile. The active/planned feature-document checks then run against
the required layout. `core` performs those checks only if the layout
already exists. Both use the reusable `Mauve.Governance.Requirements`
validation library. New `core` enrollments also receive starter lifecycle
READMEs; unlike `core-requirements`, `core` does not require the layout to
remain present. The choice cannot be silently changed by `update`.

`core` does **not** require Central Package Management in consumers, run
analyzers across their projects, check executable file kinds or workflow
pins, import TRX automatically, verify protected evidence or enforce a
complete policy. Both profiles generate the source-only index in enabled
CI or via the local-only validation procedure; an empty index is valid when no active requirements need a declared
method. .NET consumers
**should** use Central Package Management and lock files. The analyzer
and TRX tool are separately available; required source declaration checks
do not prove that the tests were discovered or run.
The managed `.editorconfig` sets analyzer diagnostic severities for .NET
projects, but it does not install `Mauve.Governance.Logging.Analyzers` or
build a consumer's projects. To enforce logging diagnostics, install that
analyzer package in the relevant projects and require their compiler/build
checks in CI. The logging analyzer checks supported Microsoft.Extensions.Logging
calls for stable templates and nonzero constant numeric event IDs at
Information, Warning, Error and Critical; diagnostic catalog content
remains a separate repository responsibility.
`governance doctor --root <repository>` reports these capability limits.
The managed consumer policy also describes a per-requirement
`docs/validation-gaps.md` ledger, repository-specific extension policies,
release supply-chain evidence and structured logging. These are
**policy guidance, not checks supplied by either current profile**, except
that active-requirement method declarations and source metadata are checked;
this does not establish observed run outcomes.
A future release-time gap report may merge that ledger with manual and
automated run reports, but no such merger or release-blocking run-evidence gap gate
exists in 0.13.0.

## Proposed extensions, not selectable profiles

Optional feature/traceability, strict .NET/AOT, prohibited-interpreter,
performance-evidence and product-boundary modules are design directions
from the [historical extraction](EXTRACTION.md), **not** supported
`--profile` values or a published profile-file schema. Their paths,
frameworks, checker pins, diagnostic domains and thresholds must remain
consumer-owned. Each extension needs a versioned schema, fail-closed
validation, its actual validator or analyzer, negative fixtures and a
required consumer CI gate before it can be documented as supported.

Do not use `--profile fleet` or invent a JSON profile alongside the
ownership manifest: the 0.13.0 tool rejects other profile names, and
unrecognized configuration is not interpreted or enforced.
