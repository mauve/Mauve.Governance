# ADR-001: Streaming, full-fidelity TRX evidence

## Status

Accepted. Implementation and verification remain separate from this decision.

## Context

The Governance TRX logger must preserve the test runner's captured execution
data, including diagnostics and attachments, while supporting large reports.
The importer needs only explicit governance metadata and truthful outcomes;
it must not reinterpret diagnostic text or source-only declarations as
execution evidence. Generic byte, result, definition, run, trait, property
and field-length budgets would reject otherwise valid runner output and
conflict with the accepted fidelity contract.

The official VSTest logger is a reference for runner fields and TRX
conventions, not a component to wrap. Its nested result layout does not
permit immediate serialization for arbitrarily ordered parent/child events.

## Decision

- Write each execution result synchronously to a single pending TRX document
  as callbacks arrive. Do not retain result objects/bodies across callbacks,
  spool result fragments or merge temporary result files.
- Emit flat results with explicit execution and parent/child identity.
  Definitions and compact linkage may remain in memory until finalization.
  Preserve every child outcome, do not double-count synthetic parent
  rollups, and retain genuine parent failures.
- Preserve runner-supplied output, messages, timing, metadata and identity
  without silent truncation. Preserve all traits and custom properties with
  their identity, type and value in a typed companion, whether or not a
  standard TRX mapping also exists. Standard well-known mappings may be
  additional TRX fields; custom properties are not flattened into unrelated
  attributes.
- Write every runner message to TRX `TextMessages` in original order with a
  textual `[Category]` prefix. Keep standard `StdOut`, `StdErr` and
  `DebugTrace` fields available to viewers. One permanent
  `<report>.files/runner-data.xml` attachment per run retains typed
  properties, exact message category/nullability/order and raw fields.
  XML-invalid code units are retained exactly in a raw companion
  representation such as base64. Raw data may duplicate TRX output and
  increase bundle storage; do not create an extra per-execution raw file.
- Copy local result and run attachments as streams into unique, relocatable
  `<report>.files/<scope>/<unique>/<name>` paths. Result TRX records
  reference the copied payloads and the companion retains attachment
  metadata. Also reference run-level payloads in
  `ResultSummary/ResultFiles`. Resolve relative result URIs from the test
  assembly directory and relative run URIs from the results directory;
  accept absolute `file:` URIs and reject other schemes without network
  access. Missing, unreadable or unsupported attachment payloads fail
  finalization.
- Publish the final TRX only after
  successful result, companion and attachment finalization. The final TRX
  marks completion but does not make multi-file bundle publication atomic.
- Import governance metadata from explicit test identity/definition links
  only. Do not open attachment paths or use companion data to repair missing
  metadata. Continue rejecting malformed XML, DTDs, external entities,
  invalid metadata and conflicting identities. Recognize results and
  definitions only in schema-designated containers; lookalike elements
  nested in output or extension content are not executions or evidence.
- Remove generic logger/importer size/count budgets on TRX/report bytes,
  executions, results, runs, definitions, messages, attachments, traits and
  properties, along with field-length and XML nesting-depth caps. Do not
  claim bounded total work or constant-memory operation. Logger memory
  scales with definitions/linkage, importer memory with retained governance
  observations/aggregates, plus the active runner result. Filesystem,
  address-space and operating-system limits still apply.

## Consequences

The output is a portable report bundle, not necessarily a standalone `.trx`.
Consumers that retain or upload evidence must retain the referenced companion
files and copied attachments with it. Full captured diagnostics can contain
secrets, so access and retention controls are required; the logger must not
silently redact or truncate requested data.

Streaming the result bodies does not make the entire pipeline constant-memory:
definition snapshots, compact linkage and imported observations remain
proportional to the evidence retained. Removing generic budgets also means
the implementation must not describe XML parsing as a bound on total work.
XML correctness, metadata validation and explicit error reporting remain
mandatory.

The importer continues to produce a governance evidence report, not a copy
of the raw logs. Duplicate identical imports remain idempotent and verdicts
retain Passed, Failed and Other observations; Skipped and NotExecuted remain
neutral. No observed test outcomes means NotObserved.

## Observed verification

The logger's 18 focused regression tests passed. Parent verification compared
real dual-logger metadata and eight real pass/fail/skip outcome runs against
the official TRX logger, including output, stack traces and attachment
payload bytes. These runs do not establish every supported runner edge case.

A separate Linux/.NET 10 session-SDK memory probe used two processes with
2,048 executions, one definition and a reused stdout payload:

| Payload size | TRX output bytes | Logger peak bytes | Total after import bytes |
| ---: | ---: | ---: | ---: |
| 512 characters | 3,859,645 | 64,720,896 | 75,993,088 |
| 16,384 characters | 68,871,357 | 65,667,072 | 77,725,696 |

The raw companion duplicates output, so the larger TRX consumes substantially
more storage. These two observations characterize only this probe; they are
not a resource bound, a constant-memory claim or a general performance
guarantee.

## Verification required

The remaining canonical suite and packaged-consumer checks must establish
full integration, bundle portability and reports beyond former
count/size/property boundaries. Keep protected hosted evidence separate.
See [FEAT-01](../Requirements/FEAT-01-Test-Run-Evidence.md) and
[FEAT-03](../Requirements/FEAT-03-Test-Metadata-And-Traceability.md).
