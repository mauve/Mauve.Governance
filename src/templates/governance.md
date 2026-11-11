# Mauve.Governance Policy

> **Managed file: do not edit directly.** Mauve.Governance replaces this
> document on a bundle update. Propose framework-wide changes to the
> [canonical Mauve.Governance Policy](https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md).
> Put repository-specific additions in documents outside
> `.mauve-governance/manifest.json`.

## MGOV-POL-001 - Enrollment and policy ownership

This repository is enrolled in the Mauve.Governance profile recorded by
`.mauve-governance/manifest.json`. That manifest specifies the selected profile
(`core` or `core-requirements`), bundle version, managed paths and
content hashes; it does not make this copy the original policy. The
[canonical policy](https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md)
owns framework-wide rules, while this document describes how a consumer
applies them. Review proposed managed-file updates rather than editing
their installed copies.

The installed `core` gate checks managed-file integrity and the structure
of feature documents *when present*. `core-requirements` additionally
requires the `docs/Requirements/` and `Planned/` lifecycle layout. Neither
profile currently enforces all sections below. A documented obligation
is not a passing CI check unless its validator and required gate exist.

### Testability and agent instruction

**Automatically testable:** `governance validate --root .` checks the selected
profile, manifest and managed-file hashes. `init` and `update` reject
unmanaged path collisions; the installed read-only workflow runs `validate`
when its pinned Tool is available. These checks do not prove that every
policy rule has a required gate.

**Agent instruction:** Inspect the manifest and CI configuration before
describing a rule as enforced; propose changes to managed files through a
reviewable update, not by editing an installed copy.

## MGOV-POL-002 - Features, planned work and architecture decisions

Keep active and partially implemented features in
`docs/Requirements/FEAT-XXXX-*.md` documents. Each active requirement
has one owning `### FEAT-XXXX-...` heading and states its behavior,
acceptance, security considerations and verification. Store proposals
and unimplemented requirements in
`docs/Requirements/Planned/FEAT-YYYY-*.md`; planned text does not
demonstrate implemented behavior or passing tests. Promote a whole
feature or individual requirement to its active feature document in the same
change as its implementation, tests, traceability, metrics/diagnostics,
status and applicable gate. Include context, metrics impact and useful
diagrams in active feature documents. A partial feature retains its
remaining drafts in
`Planned/`.

Record material architecture choices and tradeoffs in
`docs/Architecture/ADR-ZZZ-*.md`, where `ZZZ` is a numeric decision ID.
Link an ADR from the owning feature and link affected requirements from
the ADR; the ADR explains *why* a design was chosen, while a feature
specification owns *what* behavior is required. Update the feature and
relevant decisions when behavior changes; document supersession and
retirement instead of reusing IDs. See the editable
[feature lifecycle](docs/Requirements/README.md) for more detailed guidance.
Governance does not overwrite features, decisions or that README.

### Testability and agent instruction

**Automatically testable:** `governance validate --root .` checks the
structure, feature IDs, requirement IDs and required Security/Acceptance
lines of present active and planned feature documents. With
`core-requirements`, it also requires both lifecycle READMEs. It does
not verify ADR links, promotion, diagrams, metrics or actual behavior.

**Agent instruction:** Check that each promoted requirement has an owning
active document, relevant ADR links, behavioral tests and updated status;
record unverified work as planned rather than implemented.

## MGOV-POL-003 - Validation gaps and test evidence

Keep an editable [validation ledger](docs/validation-gaps.md) with a row for
**every active requirement**, including those with passing portable
tests. A requirement may have several linked validation methods.
Identify each method, its owner and procedure or test reference; classify
it as **missing**, **manual**, **portable automated**,
**environment-gated automated**, or **waived**. A portable test does not
depend on a privileged or external environment. A gated test states
the environment and enablement prerequisites; declaring it does not
mean it ran. A missing test is an explicit testing gap, not a skipped
test. A waiver requires a recorded approver, owner, reason and expiry,
and never counts as a passing test.

For example, a ledger can link `FEAT-Checkout-001` to a portable unit
test and a separate gated integration test, and identify
`FEAT-Checkout-002` as missing with an owner and next action. Aim for
automated coverage of **every** active requirement. Document manual
procedures and outstanding gaps rather than treating a manual check
as an automated pass. Do not infer observed coverage from source
annotations: a source-only index describes possible tests, and
metadata-bearing automated run reports describe actual outcomes.
Stock TRX loggers may omit feature and requirement metadata; missing
metadata is an error, not evidence of a successful run.

At release time, the **planned** validation-gap report merges the
checked-in ledger, manual-run reports and automated-run reports by
requirement. A manual report must identify the requirement,
procedure/version, operator, time, environment, artifact identity and
actual outcome. Retain provenance and distinguish Passed, Failed,
Skipped, NotExecuted, unavailable, and missing or unobserved evidence;
one result must not erase a conflicting failure. An unavailable gated
environment or missing manual report must not appear as a pass. This
merged report is **not implemented** by the current Tool or profiles;
it is report-only, not a release-blocking gap gate. Today
`governance test-results` imports metadata-bearing TRX only and cannot
invent a missing test or ingest a manual run.

### Testability and agent instruction

**Automatically testable:** `governance validate --root .` checks active
requirement method declarations against source links and the editable
ledger. Both profiles run this check and generate a fresh source-only
index in required CI without committing it. `governance test-results
--trx <run.trx> --output <report.json>` separately validates
metadata-bearing TRX and reports observed outcomes; manual reports
and the merged release-gap report are not implemented.
See the [source-only traceability guide](https://github.com/mauve/Mauve.Governance/blob/main/docs/traceability.md)
for generation and CI examples; the index ignores source line shifts and
comment-only edits.

**Agent instruction:** Compare every active requirement with the editable
ledger and actual run evidence; identify missing, gated, manual and waived
methods without turning unobserved or failed results into passes.

## MGOV-POL-004 - Repository-specific policies and annotations

If this repository adds its own policy, follow the
[extension drafting guide](docs/Policies/README.md) and keep it in a
`docs/Policies/POL-XXXX-Name-Of-Policy.md` document. Such documents
may define new repository rules and annotate generic framework rules:
cite the stable policy ID (for example `MGOV-POL-003`), then give the
local owner, scope, paths, thresholds, diagnostic namespace, required
environment and links to enforcement and evidence. State explicitly
whether each local rule has a required gate or is not yet enforced.
Annotations add context; they do not weaken mandatory framework rules
or change the managed copy. When no extension documents exist, do not
imply that repository-specific values have been configured.

### Testability and agent instruction

**Automatically testable:** No. The Tool does not parse or enforce
`POL-*.md` extensions or verify their local values and gates.

**Agent instruction:** Review each extension against its cited policy ID,
owner, scope, thresholds and evidence links; state which rules have a
required gate and do not treat a local annotation as permission to weaken
the installed policy.

## MGOV-POL-005 - Fail-closed behavior and truthful status

This repository must fail closed when a required precondition cannot be
verified. Reject the affected operation on invalid or untrusted input,
missing required configuration, failed authorization, unavailable
dependencies or unverifiable integrity. Preserve valid state and report
the failure rather than grant access, publish an unverified artifact,
continue with unsafe defaults or claim success. A documented degraded
mode may continue only when it preserves security and data integrity
and makes its limitations visible.

Apply the same principle to builds, releases and verification: do not
fabricate evidence, substitute a mock for required live verification,
or report an unavailable environment as a pass. Record what could not
be verified and why. These are repository-wide obligations, not a claim
that the installed profile currently checks every failure path.

### Testability and agent instruction

**Automatically testable:** Partly. `governance validate` fails on invalid
managed assets and feature-document inputs within its scope. The Tool
cannot establish that application authorization, dependencies, builds
or release paths all fail closed.

**Agent instruction:** Review failure and degraded-mode paths for unsafe
defaults, state changes or success-shaped fallbacks; require focused
negative tests and disclose any unverifiable environment.

## MGOV-POL-006 - Security, performance and design quality

When design goals conflict, security and performance outrank readability
and brevity. Correctness and maintainability remain mandatory and must
not be traded away. Record material choices, failure modes and security
impact in the owning feature or an ADR, supported by focused tests and
measurements. Concise code does not excuse weaker validation, excess
authority, secret exposure or unbounded work; optimization does not
excuse incorrect behavior.

### Testability and agent instruction

**Automatically testable:** No. The Tool cannot rank design tradeoffs or
validate the security and performance impact of a particular choice.

**Agent instruction:** Require the owning feature or ADR to explain
material security and performance effects, measurements, failure modes
and focused tests before preferring brevity or readability.

## MGOV-POL-007 - Supply-chain integrity

Inventory direct and transitive dependencies and review changes to
their provenance, vulnerabilities and licenses. Pin tool versions,
workflow actions and other external artifacts by immutable version,
full commit or digest; use lock files and centralized dependency
management where supported. .NET repositories should use NuGet Central
Package Management (`Directory.Packages.props`) with committed package
locks. Verify downloads and release artifacts
against trustworthy integrity material rather than trusting floating
references. A release must include an SBOM, a recorded vulnerability
and license review, and artifact attestations tied to its identity.
An enabled integrity gate rejects unverifiable artifacts; a reviewed,
time-bounded exception records owner, reason and expiry as a gap,
not as verified provenance. **The current `core` profiles do not
enforce dependency inventory, SBOM, license review or attestations.**

### Testability and agent instruction

**Automatically testable:** Partly. The Tool checks its pinned local
manifest and `update-release` verifies Governance release package digests
before installation. It does not check a consumer's MSBuild settings,
package locks, dependency inventory, SBOM, licenses or attestations.

**Agent instruction:** Inspect applicable build settings and locks,
dependency and license reviews, SBOM and artifact attestations; document
missing provenance or approved exceptions with owner and expiry.

## MGOV-POL-008 - Structured logging and troubleshooting

Use structured log fields and stable message templates rather than
interpolated, runtime-shaped messages. Information, Warning, Error
and Critical messages must carry a nonzero, stable numeric event ID
from this repository's own diagnostic namespace. Error and Critical
IDs must be unique to their failure and mapped in a diagnostic catalog
in this repository to their cause, safe public message and actionable
troubleshooting steps; do not reuse an ID for a different failure.
Bound detail and redact
secrets and sensitive data; render timestamps and identifiers
consistently so operators can correlate events without exposing
untrusted content. Record the selected ID convention and catalog
location in a repository-specific policy annotation. For C# projects,
the separately installed analyzer checks supported
Microsoft.Extensions.Logging calls, but the current `core` gate does not
build projects or enforce logging or diagnostic catalogs.

### Testability and agent instruction

**Automatically testable:** Partly, outside the Tool. A consumer that
installs `Mauve.Governance.Logging.Analyzers` and requires a C# build
can enforce constant structured templates and nonzero numeric event IDs
for supported Information, Warning, Error and Critical calls. The Tool
does not run that build or validate the diagnostic catalog or redaction.

**Agent instruction:** Check that Error and Critical IDs have distinct
troubleshooting entries, sensitive fields are redacted and the analyzer
build is actually a required gate before claiming logging compliance.

## MGOV-POL-009 - Managed validation and update workflows

The read-only `.github/workflows/governance-policy.yml` validates managed
assets and present feature documents. The framework repository builds
its Tool from source. Consumer hosted validation is disabled by default
for local-only adoption; consumers MUST run the version-pinned Tool from
a local package feed and execute `governance validate` before accepting
changes. A disabled hosted job is not passing execution evidence.
Set `GOVERNANCE_HOSTED_ENABLED` to `true` only after the pinned Tool is
available from an approved feed and require the corresponding branch
check. This gate is not yet a complete policy or test-evidence gate.

After enrollment, the scheduled update workflow checks the trusted
Mauve.Governance release, verifies package digests, reconciles owned
assets and proposes a reviewable PR. This workflow is disabled until
`GOVERNANCE_RELEASE_UPDATES_ENABLED` is set to `true`; keep it disabled
in local-only mode and review `governance update --check` and `--apply`
changes locally instead. Before enabling it, provision a repository-installed
GitHub App with Contents and Pull requests write permissions, its ID
as Actions variable `GOVERNANCE_APP_ID` and its private key as Actions
secret `GOVERNANCE_APP_PRIVATE_KEY`. The App credentials are requested
only for the PR step after read-only validation succeeds. A repository
without the installed workflow needs one-time enrollment; an unpublished
release cannot trigger an update.

### Testability and agent instruction

**Automatically testable:** Partly. `governance validate` checks the
managed workflow files against the bundle; `update-release` verifies
release digests, reconciles owned paths and stages changes. Neither
command proves that hosted checks are required, App credentials work
or update PRs have actually run.

**Agent instruction:** Verify the trusted feed, required branch check,
App permissions and secret configuration; observe a real update PR
before claiming scheduled rollout works.
