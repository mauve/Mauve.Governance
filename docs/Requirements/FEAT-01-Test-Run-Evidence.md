# FEAT-01: Test-run feature evidence

## Status

**PARTIAL**. The TRX reader, merge library, `test-results` subcommand and
metadata-bearing VSTest logger are implemented locally. Real MSTest,
xUnit v2/v3 and NUnit runs verify parameterized pass/fail, skips and reruns.
Protected hosted execution and artifact provenance remain planned.

## Capability

Aggregate observed test outcomes by owning feature and requirement across
multiple TRX files, including an existing report and newly supplied runs.

## Context

Static source traceability describes possible coverage; TRX describes what
was actually observed. The run report MUST NOT fill missing TRX metadata
from source files or display names. See the canonical
[Mauve.Governance Policy](../governance.md).

## Requirements

### FEAT-01-001 - Bounded and truthful TRX evidence

The reader MUST reject malformed, unbounded and external-entity XML, and
tests with missing, duplicate or cross-feature metadata. It MUST retain
individual run identities, test outcomes and digests; duplicate byte-identical
imports MUST be idempotent.

**Acceptance:** parser fixtures reject invalid metadata/XML and the CLI
reports a coded error without writing a success-shaped report.

**Security:** untrusted test output cannot become a fake feature pass or
force unbounded parse work.

### FEAT-01-002 - Order-independent result merging

Verdicts MUST derive from retained Passed, Failed and Other observations.
Skipped/NotExecuted observations MUST remain visible but neutral, and
skipped-only features MUST be NotObserved. Passed+Failed is Mixed,
Passed+Other is Indeterminate, and Failed+Other is Failed.

**Acceptance:** fixture tests cover order independence, a same-file rerun,
incremental imports and duplicate suppression at test, requirement and
feature granularity.

**Security:** a later passing scenario cannot erase a prior failure.

### FEAT-01-003 - Verified metadata-bearing TRX for each framework

The VSTest logger MUST preserve runner-provided feature and requirement IDs
in TRX for MSTest, xUnit v2/v3 and NUnit, including parameterized tests,
skips, failures and distinct reruns. Evidence MUST come from the actual TRX,
never from a source-only traceability index.

**Acceptance:** the real-runner metadata and outcome scripts import all four
frameworks' TRX, and the outcome script verifies eight distinct runs, each
with a pass, failure and skip under the owning feature.

**Security:** missing or contradictory metadata fails extraction rather than
being inferred from a test name or source annotation.

## Metrics Impact

No runtime metrics are introduced. The tool emits bounded report counts,
not telemetry; no user-controlled test output becomes a metric dimension.

## Verification

`Mauve.Governance.TestResults.Tests` exercises XML security, metadata
validation and merge transitions. `tests/verify-trx-metadata.sh` imports
11 real runner results, and `tests/verify-trx-outcomes.sh` verifies local
failure, skip and rerun evidence for all four frameworks. Protected hosted
provenance remains unverified.

## Diagrams

No placement diagram is necessary for this library-only parsing/aggregation
feature; the input TRX and output JSON are explicit in its CLI contract.
