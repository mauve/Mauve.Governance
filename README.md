# Mauve.Governance

Mauve.Governance has its own canonical
[Governance Policy](docs/governance.md). Its earliest requirements
were informed by Mauve.Fleet's repository-governance work; Fleet's feature
identifiers and appliance rules are not the framework's policy.
Features and proposed requirements in this repository use the
[`docs/Requirements/` lifecycle](docs/Requirements/README.md), which is
also the recommended layout for consumers.
The framework is under construction; a rule is not enforced until its
validator or analyzer is implemented and made a required consumer gate.

## Ownership boundaries

The policy calls for versioned profiles, pinned checks,
test-to-feature traceability, truthful evidence and reviewable repository
updates. Fleet-specific BAM, appliance, Azure, kernel, product-root, diagnostic
and performance settings belong in an optional consumer profile; they are
not universal framework defaults. No Fleet project is a build dependency.
The [historical extraction matrix](docs/EXTRACTION.md) maps Fleet's source
requirements to portable mechanisms and consumer-owned choices.
The [profile reference](docs/profiles.md) documents the supported `core`
and `core-requirements` profiles and distinguishes proposed extensions
from selectable profiles.
The [bootstrap guide](docs/bootstrap.md) covers new repositories, known
older manifests, unmanaged collisions and update enrollment.
The [release guide](docs/release.md) explains package distribution and
the consumer-side update prerequisites.
Consumer-distributed governance documents, skills, agents, workflows and
the pinned tool manifest are authored once in `src/templates/` and embedded
in the Tool. The canonical policy is authored in
`src/templates/docs/governance.md`; the Tool installs `docs/governance.md`
here and in consumers. The root README and feature documents remain
authored here, not copied from those templates.
This repository uses NuGet Central Package Management through
`Directory.Packages.props` and committed package lock files; .NET consumers
should adopt the same convention for reviewable version changes.
The [Governance workflow](.github/workflows/governance.yml) and related
workflow definitions are not evidence of a hosted consumer run. Main requires
the GitHub Actions `governance` and `managed-assets` checks on up-to-date PRs,
including for administrators; approval count is zero for this single-maintainer
repository. Local build, test and validation commands are available; full
policy enforcement is not implemented. See the
[local consumer check](docs/release.md#local-package-consumer-check).

Reusable libraries include `Mauve.Governance.TestResults` for TRX-only
test-run evidence and `Mauve.Governance.Requirements` for bounded
feature-document validation. `Mauve.Governance.Testing.Analyzers` checks
test metadata; the separate `Mauve.Governance.Logging.Analyzers` package
checks structured Microsoft.Extensions.Logging calls. Consumers install
only the analyzer packages they need and require a project build to enforce
their diagnostics; the managed `.editorconfig` alone does not run them.
`Mauve.Governance.Tool` is the sole .NET
tool and supports `test-results`, `init`, `update`, `update-release`,
`validate`, `doctor` and `generate`. Local `update` reconciles files only;
hosted release discovery and update PR automation are deferred. **`validate` checks managed-file
integrity and basic feature
document structure when `docs/Requirements/` exists**, not the full
Mauve.Governance Policy. A full policy validator, verified hosted update
automation remain planned; framework PR gates do not establish consumer readiness.

To enroll an existing directory, use
`governance init --root <path> --profile core --bundle-version 0.13.0`
(or add `--check` to preview). This installs a version-pinned
`.config/dotnet-tools.json`, managed governance workflows, repo-local skills and custom agents and governance
documentation, including a [consumer-facing policy copy](governance.md),
recording their ownership
in `.mauve-governance/manifest.json`. The external hosted policy job is
disabled unless `GOVERNANCE_HOSTED_ENABLED` is exactly `true`; update PR
automation is disabled unless `GOVERNANCE_RELEASE_UPDATES_ENABLED` is exactly
`true`. Neither opt-in removes the trusted package, release and App
prerequisites. `governance update --root <path> --check`
reports drift; `--apply` changes managed files locally for review in a PR.
The copied policy and governance documentation link to the
[original policy source](src/templates/docs/governance.md) and explicitly say not to edit
managed copies; consumer-specific guidance belongs in separate files.
Known 0.2.0 through 0.12.0 manifests migrate; unmanaged collisions block
migration and are never adopted implicitly. No public Governance NuGet
release or hosted update run is currently verified. The
[`Mauve.Governance.MSTest.Example`](examples/Mauve.Governance.MSTest.Example/)
directory demonstrates repository-local copied agent and skill assets; it
is not a substitute for full policy validation. The managed workflow checks owned files, present feature documents and
declared source/ledger methods; this describes intended workflow behavior,
not observed hosted consumer evidence. It does not import observed test-run
evidence.

Run `bash tests/verify-local-consumer.sh` for an isolated local-feed
end-to-end consumer check. It exercises freshly created MSTest consumers,
copies the SDK pin, prevents inherited build/editor settings, restores the
pinned tool manifest, uses packaged analyzers/adapters/logger, imports
metadata-bearing TRX, generates source-only traceability, checks both
profiles, no-op update behavior and editable-file preservation. This
synthetic local check is not acceptance from an actual
product consumer or proof of a hosted workflow. The current Tool, MSTest
metadata adapter and TRX logger target `net10.0`; .NET 10 is the minimum
verified consumer target for these packages. The two analyzers target
`netstandard2.0`.

The source checkout requires the SDK pinned in `global.json` (`10.0.404`).
Use the per-project runner commands in the Governance workflow rather than
unfiltered solution-wide `dotnet test`: the solution includes an intentional
failure fixture, and xUnit v3 uses its standalone runner for normal tests
and VSTest for metadata-bearing TRX.

`governance generate --root <path> --output <trace.json>` creates a
[source-only traceability index](docs/traceability.md);
`--check` compares it with a previously generated file for repositories
that choose to keep one. Local generation is available to consumers;
hosted generation artifacts are not treated as verified until the relevant
workflow is explicitly enabled and observed. This is **not** runner
discovery or observed coverage.
The index omits source line numbers so comment-only edits do not alter it;
the linked guide shows local and CI workflows with examples.

## Tool option reference

Run `governance <command> --help` for the same option descriptions in
the CLI. Required options are marked **required**. Paths are relative to
the current working directory unless absolute.

| Command | Option | Purpose |
| --- | --- | --- |
| `init` | `--root <directory>` | **Required.** Existing directory to enroll. |
| `init` | `--profile <name>` | **Required.** Select `core` or `core-requirements`. |
| `init` | `--bundle-version <version>` | **Required.** Exact current Governance bundle version. |
| `init` | `--check` | Preview without writing; exits nonzero when files would change. |
| `update` | `--root <directory>` | **Required.** Enrolled repository to reconcile. |
| `update` | `--check` | Show pending changes without writing; choose exactly one of `--check` or `--apply`. |
| `update` | `--apply` | Apply changes to manifest-owned files locally; choose exactly one of `--check` or `--apply`. |
| `validate` | `--root <directory>` | **Required.** Enrolled repository to check for managed-file and applicable feature-document violations. |
| `doctor` | `--root <directory>` | **Required.** Enrolled repository whose enforced and unavailable capabilities to report. |
| `generate` | `--root <directory>` | **Required.** Source tree to scan for test metadata. |
| `generate` | `--output <file>` | **Required.** Destination for the source-only JSON index. |
| `generate` | `--check` | Compare against the existing index without writing; exits nonzero if missing or stale. |
| `test-results` | `--trx <file>` | **Required.** Metadata-bearing TRX file; repeat to import multiple runs. |
| `test-results` | `--input-report <file>` | Existing evidence report to extend; omit to start a new one. |
| `test-results` | `--output <file>` | **Required.** JSON evidence report; may equal `--input-report`, but not a TRX input. |
| `update-release` | `--root <directory>` | **Required.** Enrolled Git repository to update from the trusted release; requires `GITHUB_OUTPUT` in the update workflow. |

## Test-run evidence contract

Run evidence comes exclusively from metadata included in each TRX file.
Each executed result requires exactly one `Feature` property and optionally
multiple distinct `Requirement` properties with matching identifiers. The
initial grammar accepts `FEAT-` followed by a bounded ASCII letter/digit
identifier (with internal `-`, `_` or `.`), and requirement IDs extend
the owning feature plus a similarly bounded suffix; fixed-width Fleet
numbering is not imposed on other consumers. The source traceability index cannot substitute
for missing run metadata.
Stock TRX loggers omit traits and test properties. The packable
[`Mauve.Governance.Testing.TrxLogger`](src/Mauve.Governance.Testing.TrxLogger/README.md)
reads runner traits without looking up source, emitting real TRX usable by
`test-results` for MSTest, xUnit v2/v3 and NUnit. The four-framework
[fixture script](tests/verify-trx-metadata.sh) verifies the observed mapping;
xUnit v3 uses its VSTest adapter for logger loading.
An [outcome fixture](tests/verify-trx-outcomes.sh) separately verifies real
parameterized pass/fail, skipped and repeated runs for all four frameworks.
Skipped and NotExecuted outcomes are recorded but do not contribute to a
feature verdict. Reports retain runs and test outcomes to permit reviewable
incremental merging.

| Observed outcomes | Verdict |
| --- | --- |
| Only skipped or NotExecuted | NotObserved |
| Passed (with any neutral outcomes) | Passed |
| Failed, or Failed and Other | Failed |
| Other, or Passed and Other | Indeterminate |
| Passed and Failed (with or without Other) | Mixed |

The report combines outcome evidence at test, requirement and feature levels.
Re-importing byte-identical TRX content is idempotent. Different runs remain
separate evidence; a displayed verdict never erases a failed observation.
