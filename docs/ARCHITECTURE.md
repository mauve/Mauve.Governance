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
test metadata in the TestResults test project and bounded TRX evidence
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
