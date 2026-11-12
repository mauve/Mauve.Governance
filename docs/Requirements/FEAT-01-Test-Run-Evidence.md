# FEAT-01: Test-run feature evidence

## Status

**PARTIAL**. The TRX reader, merge library, `test-results` subcommand and
metadata-bearing VSTest logger have local implementations and fixture
coverage. The streaming logger's 18 focused regression tests pass. Parent
verification also compared real dual-logger metadata and eight real
pass/fail/skip outcome runs, including output, stack traces and attachment
bytes. Full importer integration and all-corner-case fidelity verification
remain incomplete. Protected hosted execution and artifact provenance remain
planned.

## Capability

Aggregate observed test outcomes by owning feature and requirement across
multiple TRX files, including an existing report and newly supplied runs.

## Context

Static source traceability describes possible coverage; TRX describes what
was actually observed. The run report MUST NOT fill missing TRX metadata
from source files or display names. See the canonical
[Mauve.Governance Policy](../governance.md).

## Requirements

### FEAT-01-001 - XML-safe and truthful TRX evidence

The reader MUST reject malformed XML, DTDs, external entities, and tests
with missing, duplicate or cross-feature metadata. It MUST retain individual
run identities, test outcomes and digests; duplicate byte-identical imports
MUST be idempotent. Implementations MUST NOT impose generic size/count
budgets on TRX/report bytes, executions, results, runs, definitions,
messages, attachments, traits or properties. They MUST NOT cap field length
or XML nesting depth, or claim bounded total work or constant-memory
processing. XML safety, metadata integrity and truthful failure behavior
remain required. Import memory scales with retained governance observations
and aggregates; the evidence reader does not retain diagnostic output as
evidence.

**Acceptance:** parser fixtures reject invalid metadata/XML and the CLI
reports a coded error without writing a success-shaped report. Large
diagnostic payloads and reports beyond former size/count limits must round
trip without being copied into aggregate JSON. Deeply nested irrelevant
output/definition content, including XML elements that resemble result
records, must not be interpreted as additional tests or metadata.

**Security:** untrusted test output cannot become a fake feature pass.
Removing generic resource budgets is not a claim that work or memory is
bounded; operational failures must be explicit, not converted to truncation,
omission or a success-shaped report.

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

The VSTest logger MUST preserve all execution data supplied by MSTest,
xUnit v2/v3 and NUnit without silent truncation or omission: outcomes,
timing/duration, execution and parent/child identity, run completion state
and summary, diagnostic output, messages, metadata and local attachments.
It MUST emit flat executions whose explicit
relationships preserve child observations and genuine parent failures
without counting synthetic parent rollups twice. Nonstandard runner data
MUST be represented losslessly in one referenced, versioned
`<report>.files/runner-data.xml` companion per run. It preserves all runner
properties with identity/type/value (even when standard TRX mappings also
exist), exact message category/nullability/order, and raw fields. Standard
well-known property mappings may additionally appear as TRX fields; custom
properties are not flattened into unrelated attributes. Every message MUST
appear in TRX `TextMessages` in original order with a textual `[Category]`
prefix; standard `StdOut`, `StdErr` and `DebugTrace` remain available to
viewers. XML-invalid code units MUST be preserved exactly in raw companion
form such as base64. This raw data can duplicate standard output and increase
storage; no additional per-execution raw file is used.

Local result/run attachment payloads MUST be copied into unique relocatable
`<report>.files/<scope>/<unique>/<name>` paths, referenced from result
records, with attachment metadata in the companion. The logger MUST stream
results into one pending TRX document and publish the final TRX only after
successful finalization; it MUST NOT merge temporary result fragments.
Run-level attachments are also referenced in `ResultSummary/ResultFiles`;
relative result URIs resolve from the test assembly directory and relative
run URIs from the results directory. Absolute `file:` URIs are supported;
non-file schemes are rejected without network access.
Unsupported values or data that cannot be represented losslessly MUST fail
explicitly. Evidence MUST come from explicit metadata in the actual TRX,
never from a source-only traceability index, display names or diagnostic
text.

**Acceptance:** focused regressions compare complete runner data and
portable attachments, including message ordering/categories, raw invalid XML
code units and typed properties; exercise flat parent/child semantics and
reports beyond former budgets; and verify no final TRX appears before
successful publication. Real runner verification covers the four
frameworks' metadata and outcomes.

**Security:** missing or contradictory metadata fails extraction rather than
being inferred from a test name or source annotation. Full diagnostic output
may contain secrets; preserve it as supplied and apply appropriate report
bundle access controls rather than silently redact it.

## Metrics Impact

No runtime metrics are introduced. Report counts describe evidence, not
telemetry; no user-controlled test output becomes a metric dimension. Logger
memory scales with definitions and compact execution linkage; importer memory
scales with retained observations and aggregates. Neither pipeline promises
constant memory or bounded total work, and operating-system limits still
apply.

## Verification

`Mauve.Governance.TestResults.Tests` exercises XML security, metadata
validation and merge transitions. Parent verification observed both real
dual-logger metadata comparison and eight real outcome runs across the four
frameworks; the outcome comparison includes output, stack traces and
attachment bytes against the official logger. These results do not alone
establish every full-fidelity case or protected hosted provenance.
`tests/verify-local-consumer.sh` additionally restores the packaged Tool,
adapter and logger in an isolated external MSTest consumer and imports a
real passing TRX for each profile. This is local package integration evidence,
not product-consumer acceptance or protected provenance.

## Diagrams

No placement diagram is necessary for this library-only parsing/aggregation
feature; the input TRX and output JSON are explicit in its CLI contract.
