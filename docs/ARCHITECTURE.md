# Governance framework boundaries

## Policy ownership

The [Mauve.Governance Policy](governance.md) is the independent
normative contract. Fleet's earlier governance work is historical
extraction context, not a policy every consumer inherits. Framework
components must not reference `Mauve.Fleet*`.
Reusable mechanisms accept an explicit consumer profile; requirements whose
meaning depends on appliance, BAM, Azure, kernel, product-root diagnostic
domains or specific baselines stay in consumer-owned configuration. A profile
is not a substitute for its validator and required CI gate.

| Rule family | Portable mechanism | Consumer-owned inputs |
| --- | --- | --- |
| Documents and diagnostics | Requirement and diagnostic catalog checks | Paths, identifier formats and domains |
| Builds and automation | Registered checkers, immutable pins and thin workflows | Languages, SDK, actions, release infrastructure |
| Test traceability | Framework-specific adapters and independent run evidence | Requirements, framework/runner, supported TRX reporter |
| Performance and protected evidence | Evidence schemas and truthful gap status | Critical paths, thresholds, privileged lanes |
| Names and dependencies | Configurable namespace/product graph checks | Platform roots, allowed edges and prefixes |
| Agent assets | Versioned, copied, manifest-owned repository files | Consumer-authored additions outside managed paths |

All consumer-distributed agents, skills, workflows, pinned tool manifest and
governance document copies are sourced from `src/templates/` and embedded
in the sole Tool package. `init`/`update` generate the Governance repository's
own managed copies as they would a consumer's. The canonical policy is
authored in `src/templates/docs/governance.md` and installed at
`docs/governance.md`; the root README, other framework documentation and
feature specifications remain maintainer-authored. The ownership
manifest is deterministic, two-space-indented JSON with content hashes.
`Mauve.Governance.Requirements` provides the reusable, bounded Markdown
document validation used by the Tool when the selected profile requires
the feature lifecycle or when a `docs/Requirements/` directory exists.

Managed-file validation, basic feature-document structure, compile-time
test metadata in the TestResults test project and XML-safe TRX evidence
parsing are implemented and checked in this repository's own CI workflow.
Local equivalents pass; main requires the GitHub Actions `governance` and
`managed-assets` checks through up-to-date PRs, including for administrators.
Those repository checks do not prove product-consumer or protected evidence.
The full policy validator, profile schema,
hosted update-PR verification and protected runner-outcome coverage remain
planned;
`governance validate` must not be used as a full Governance policy gate.

## Evidence layers

Static traceability states what a test could verify; a TRX run states what
was actually observed. They cannot stand in for each other. Stock VSTest
TRX output drops native metadata in the pinned runners; the Governance
VSTest logger preserves traits exposed by the runner, and real TRX import
now works for MSTest, xUnit v2/v3 and NUnit fixtures. The TRX reader accepts
recorded `Feature` and
`Requirement` properties only, preserves per-run observations, and rejects
results without metadata. Its JSON report is derived evidence, not a signed
attestation; source integrity and protected lanes need separate provenance.

## TRX fidelity and resource model

The approved logger contract preserves the runner-supplied execution record
without silent truncation: outcomes, timing, execution and parent/child
identity, messages, diagnostic output, metadata and local attachments.
Execution results are flat and explicitly linked so callbacks may arrive
out of parent order. Governance verdicts must retain child outcomes, avoid
counting synthetic parent rollups twice, and preserve a genuine parent
failure. Every runner property is preserved with identity, type and value in
a typed companion, whether or not it also has a standard TRX mapping;
standard well-known mappings may be emitted as additional TRX fields.
Custom properties are not silently flattened into unrelated standard
attributes. Data without a standard TRX slot also belongs in the companion.

Every runner message is written to TRX `TextMessages` in original order with
a textual `[Category]` prefix. Standard `StdOut`, `StdErr` and `DebugTrace`
fields remain available to viewers. A permanent
`<report>.files/runner-data.xml` attachment preserves typed properties,
message category/nullability/order and raw fields. XML-invalid code units
are retained exactly in a raw companion representation such as base64. Raw
output may therefore be duplicated in the companion and increase bundle
storage; no separate per-execution raw file is used. Local result and run
attachments are copied under unique relocatable
`<report>.files/<scope>/<unique>/<name>` paths;
result records reference copied payloads and the companion retains their
metadata. Run-level payloads are also referenced in standard
`ResultSummary/ResultFiles`. Relative result attachment URIs resolve from
the test assembly directory; relative run attachments resolve from the
results directory. Absolute `file:` URIs are accepted; non-file network
locations are rejected and never fetched implicitly.

One pending TRX document is finalized before the final
`.trx` is published; the final file is the completion marker, not an atomic
publication guarantee for the entire directory. Temporary result fragments
are not merged.

The logger retains immutable definition snapshots and compact execution
linkage until finalization. The importer retains governance metadata,
observations and aggregates. Memory therefore scales with these retained
structures and the current runner result; neither side promises constant
memory or bounded total work. No generic size/count budget on TRX/report
bytes, executions, results, runs, definitions, messages, attachments,
traits or properties, field-length cap or XML nesting-depth cap is part of
the TRX contract.
XML well-formedness, DTD/external-entity rejection, metadata integrity,
identity consistency and explicit failure behavior remain mandatory.
Importers read only the metadata needed for governance evidence and never
open attachments or treat diagnostic text as evidence.

Captured test output and copied attachments may contain secrets. Preserve
runner data faithfully rather than silently redact it; repositories must
control access, retention and distribution of the complete report bundle.
See [ADR-001](Architecture/ADR-001-Streaming-TRX-Evidence.md) for the
decision, observed memory probe and real-runner comparisons. Those
measurements cover only their described fixtures and are not resource bounds
or general runner guarantees.

## Update ownership

Bootstrap requires an explicit supported profile and bundle version, installs
a pinned local tool and read-only managed-assets check, and refuses collisions
with existing consumer files. Local-only consumers install locally packed
packages from an isolated feed; nothing is published. Hosted consumer
validation is disabled until explicitly enabled with
`GOVERNANCE_HOSTED_ENABLED=true` and an approved feed can restore the
pinned Tool. The framework's hosted policy job builds the Tool from source.
Update changes only paths
owned by the manifest, and replaces
an edited owned file in the proposed change while preserving all unmanaged
files. A consumer without an installed workflow cannot self-discover new
releases; the bundled scheduled/manual workflow is disabled until
`GOVERNANCE_RELEASE_UPDATES_ENABLED=true`. Once explicitly enabled against
a published release, it calls the pinned Tool's
`update-release` command to verify release digests, install the exact
released Tool, reconcile owned paths and stage a validated diff in a
read-only checkout. The workflow requests a least-privilege GitHub App
token only afterward to propose a PR. The former shell updater is not
shipped in new bundles and is removed only from manifests that own it.
This workflow has not run against a published release. The local
`update --apply` still requires a maintainer to propose a PR manually.
Never run untrusted pull-request code with repository write credentials.
