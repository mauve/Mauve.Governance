# Mauve.Governance Policy

> **Managed file: do not edit directly.** Mauve.Governance replaces this
> document on a bundle update. Propose framework-wide changes to the
> [original policy source](https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md);
> put repository-specific additions outside `.mauve-governance/manifest.json`.

This is the canonical repository-governance policy for the Mauve.Governance
framework. Keywords **MUST**, **MUST NOT**, **SHOULD** and **MAY** are
normative. Profiles define which optional modules a consuming repository
adopts; selecting a profile never implies an unenforced rule is passing. The
[profile reference](https://github.com/mauve/Mauve.Governance/blob/main/docs/profiles.md) identifies the only supported selection and
its current enforcement boundary.

## Status

**PARTIAL**. Managed-file integrity, basic feature-document structure and TRX evidence parsing have
working libraries/tools and focused tests required by the Mauve.Governance
repository's CI workflow. Those checks passed locally; branch-protected execution has
not been verified. This does not yet form a complete consumer policy gate.
The `core` and `core-requirements` bundles record an explicit profile/version and install a read-only
consumer gate for managed files and present documents. In local-only mode,
consumers run the pinned Tool from a locally packed feed; hosted consumer
validation is disabled until `GOVERNANCE_HOSTED_ENABLED` is explicitly set
to `true` and the pinned Tool is available from an approved feed.
The framework's hosted gate builds the Tool from source instead.
Additional policy
profile modules, full analyzers, hosted update
rollout and protected
environment checks are **PLANNED**. `governance validate` checks currently
managed files and present feature-document structure; it MUST NOT be
represented as full policy validation.

## Framework requirements

### MG-POL-001 - Ownership and profiles

Governance MUST have one canonical policy source in the Mauve.Governance repository and
versioned, explicit consumer profiles. A rule MUST NOT be reported as
implemented until its validator/analyzer is required in the consumer's CI
gate. Consumer-specific platform, diagnostic, SDK, feature-ID and performance
values MUST be specified by each repository. Repositories MUST reject
affected operations when required inputs, authorization, dependencies or
integrity cannot be verified, preserve valid state and report the failure
rather than claim success. A documented degraded mode MAY continue only
if it preserves security and data integrity and discloses its limits.
Unknown profile schema and rules MUST fail closed in enabled checks.
Consumers MAY add their own rules and
annotate generic rule IDs with local owners, scope, paths, thresholds and
enforcement links in
`docs/Policies/POL-XXXX-Name-Of-Policy.md` files; they MUST NOT edit a
managed policy copy or represent an extension without a gate as enforced.
**Two fixed profiles and manifest checks: IMPLEMENTED locally.
Repository-wide failure behavior, configurable profile schema and
optional rule modules: not enforced by current profiles.**

### MG-POL-002 - Source, workflow and supply-chain checks

A selected profile MUST register each executable/build file kind with an
applicable pinned check, validate thin fixed-argument workflows and reject
floating actions/downloads. Compiler, analyzer, lint and documentation
warnings MUST fail enabled gates. Suppressions MUST be narrow and
evidence-backed. The framework repository MUST use NuGet Central Package
Management and committed lock files; .NET consumer repositories SHOULD use
Central Package Management to make dependency version review consistent.
Repositories MUST inventory direct/transitive dependencies, pin external
tools, actions and artifacts, check integrity, and review vulnerability,
license and provenance changes. A release MUST include an SBOM,
vulnerability/license review and artifact attestation tied to its identity.
An enabled integrity gate MUST reject unverified artifacts unless an
explicit, reviewed, time-bounded exception with an owner and reason is
recorded as a gap, not as successful verification. **Framework package
management and local updater digest checks: IMPLEMENTED. Consumer
supply-chain and release-evidence enforcement: PLANNED.**

### MG-POL-003 - Test metadata and traceability

Test adapters MUST support separate MSTest, xUnit v2, xUnit v3 and NUnit
packages without replacing their native test runners. A method-level test
MUST identify one owning feature, MAY identify multiple distinct matching
requirements, and MAY use either native or verified convenience metadata
attributes. Invalid, absent or cross-feature metadata MUST fail enabled
gates. Consumer feature documents SHOULD follow the
[`docs/Requirements/` lifecycle](Requirements/README.md): designs in
`Planned/` are not active evidence, and promotion accompanies behavioral
tests, status, diagnostic/metrics and architecture updates. Numeric
`docs/Architecture/ADR-ZZZ-*.md` decisions explain material choices
and link to affected features; an ADR does not own a requirement.
Both installed profiles MUST check fresh source declarations against active
requirements and an editable `docs/validation-gaps.md` ledger in their
enabled validation gate. Local-only consumers MUST run `governance validate`
before accepting changes; an explicitly disabled hosted job is not a
passing or required CI gate. Each active requirement MUST have a ledger row and
either a declared C# source test or a validated repository-local alternative
procedure or approved, unexpired waiver. A `missing` method alone does not
satisfy this gate. For release evidence, `docs/validation-gaps.md` SHOULD
record each applicable missing, manual,
portable automated, environment-gated automated or approved waived
validation method. Multiple methods may cover one requirement; a
waiver never counts as a passing test. Static traceability MUST NOT
be reported as executed evidence.
**Basic document structure, metadata analysis in the TestResults test
project, and a source-only generator with a self-hosted and consumer
declaration gate: IMPLEMENTED locally. Observed execution and
release-time evidence merging remain separate.**

### MG-POL-004 - Observed test evidence

Test-run evidence MUST come from metadata present in the supplied TRX,
never from test names or a static traceability index. Missing/ambiguous
metadata MUST fail extraction. Test, requirement and feature verdicts MUST
merge by retaining Passed, Failed and Other observations; Skipped and
NotExecuted remain counted but neutral. No observations, including
skipped-only, mean NotObserved. Passed+Failed means Mixed; Passed+Other
means Indeterminate; Failed+Other means Failed. Distinct run provenance
MUST be retained, duplicate identical TRX imports MUST be idempotent,
and imports MUST be bounded and reject external XML entities.
The future release-time validation-gap report SHOULD merge the
consumer's requirement ledger, automated run reports and manual-run
reports, retaining requirement ID, procedure/version, operator, time,
environment, artifact identity and actual outcome for manual runs.
Missing tests and unavailable environments MUST remain distinct from
passing evidence. That merger is **report-only** until a separately
implemented release gate is selected; it is not supplied by the
TRX-only `test-results` command.
**Parser/merge and VSTest trait logger with local pass/fail/skip/rerun
verification for all four fixture frameworks: IMPLEMENTED locally.
Manual reports, release-time merger and protected hosted evidence:
PLANNED.**

### MG-POL-005 - Managed assets and updates

Bootstrap MUST require explicit enrollment. Skills and custom agents MAY
be copied into a consumer's repository as reviewed, manifest-owned files.
Consumer-distributed files, including this canonical policy, MUST have
one authored source under Mauve.Governance's `src/templates/`; the Tool
embeds them and installs the same assets into the framework repository.
Consumer-authored feature
documents MUST NOT be regenerated as managed copies.
The ownership manifest MUST use stable, readable two-space-indented JSON.
Updates MUST replace only owned paths, preserve unmanaged files, fail
on unmanaged collisions and be proposed as reviewable PRs. A repository
without an updater workflow requires one-time enrollment; an updated bundle
MUST NOT silently rewrite its default branch. **Local bootstrap, migration of known 0.2.0-0.9.0 bundles, pinned tool
manifest, linked non-editable consumer documents and agents/skills, read-only consumer gate
and owned-file checks: PARTIAL. A
digest-checking, GitHub-App-authorized consumer updater is bundled; a trusted
release and hosted PR verification are still required.**

### MG-POL-006 - Truthful diagnostics and evidence

Visible tool errors MUST carry stable codes. Production logging MUST use
structured fields and stable message templates, bound detail, redact
sensitive data and render timestamps consistently. Information, Warning,
Error and Critical messages MUST carry a stable, nonzero numeric event ID
from the repository's own diagnostic namespace. Error and Critical IDs
MUST uniquely identify their failure and be mapped by a diagnostic
catalog in that repository to its cause, safe message and
troubleshooting instructions. Protected
environment outcomes, metrics and performance claims MUST distinguish
observed success from skips, mocks and unavailable infrastructure.
Consumer diagnostic namespaces, status vocabulary, threshold values
and gap-ledger paths belong in repository-specific policy annotations.
**Explicit core enrollment and Tool error codes: PARTIAL. The
separately installed C# analyzer checks supported logging calls;
consumer-wide logging/catalog gates and additional profile rule
enforcement: PLANNED.**

### MG-POL-007 - Security and performance tradeoffs

When goals conflict, security and performance MUST outrank readability
and brevity; correctness and maintainability MUST still be preserved.
Material design tradeoffs MUST be recorded in the owning feature or
numeric ADR with failure modes, focused tests and measurements.
Neither concise code nor a performance improvement justifies weaker
validation, excess authority, secret exposure or incorrect results.
**Review/measurement enforcement: PLANNED.**

## Architecture and evidence

See the framework's
[architecture](https://github.com/mauve/Mauve.Governance/blob/main/docs/ARCHITECTURE.md)
for consumer boundaries and run provenance; its
[README](https://github.com/mauve/Mauve.Governance/blob/main/README.md)
documents shipped commands.
