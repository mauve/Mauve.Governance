# Governance TRX logger

The stock VSTest TRX logger does not include the test-case traits needed by
`TrxEvidence`. Reference this package in each test project and enable the
VSTest logger explicitly: `dotnet test tests/MyTests.csproj --logger governance-trx`.
For xUnit v3 under the .NET 10 SDK, build with its VSTest adapter and use
`dotnet vstest <test-assembly.dll> /Logger:governance-trx`; the standalone
in-process runner does not load VSTest loggers.

The logger records the runner's execution data without silently truncating
or omitting it: outcomes, timing, execution and parent/child identity,
messages, diagnostic output, metadata and local attachments. Flat execution
records carry explicit relationship IDs and do not depend on parent callbacks
arriving first. Test definitions retain the runner's identity and metadata.
Feature and Requirement properties remain runner-supplied; the logger does
not load source, infer metadata from display names or manufacture absent
values.

Every runner property is retained with identity, type and value in the
typed companion, including properties that also map to standard TRX
fields. Standard well-known mappings may be emitted as additional TRX
fields; custom properties are not flattened into unrelated standard
attributes.

Every runner message is written to TRX `TextMessages` in its original order
with a textual `[Category]` prefix. Standard `StdOut`, `StdErr` and
`DebugTrace` fields remain available to TRX viewers. One permanent
`<report>.files/runner-data.xml` attachment per run retains typed properties,
the exact message category/nullability/order and raw fields. XML-invalid
code units are preserved exactly in a raw companion representation such as
base64. Raw fields may duplicate standard TRX output, increasing bundle
storage; the logger does not create an additional per-execution raw file.

Local result- and run-level attachment payloads are copied to unique,
relocatable paths under `<report>.files/<scope>/<unique>/<name>`. Result
records reference their copied attachments, and the companion retains
attachment metadata. Run-level payloads are also referenced in standard
`ResultSummary/ResultFiles`. Relative result attachment URIs resolve from
the test assembly directory; relative run attachments resolve from the
results directory. Absolute `file:` URIs are accepted; other schemes are
rejected without network access. Keep the `.trx` and every file it references
together when moving or uploading a run.
The final TRX is published only after successful finalization and acts as the
run's completion marker; the entire multi-file bundle is not an atomic
filesystem transaction. Unsupported values, unreadable attachments or data
that cannot be represented losslessly fail explicitly.

The logger and evidence importer impose no generic size/count budgets on
TRX/report bytes, executions, results, runs, definitions, messages,
attachments, traits or properties, and no field-length or XML nesting-depth
caps. Streaming result bodies does not mean constant memory: the logger
retains definition metadata and compact
execution linkage until finalization, and the importer retains governance
observations and aggregates. Memory therefore scales with these retained
structures and the current runner result; disk and operating-system limits
still apply.

The evidence importer reads only the explicit Feature/Requirement metadata
needed for governance verdicts. It does not open attachments or use
diagnostic output, source indexes or companion payloads to repair missing
metadata. A source-only traceability index is separate from execution
evidence.

Captured test output and attachment payloads may contain secrets. Preserve
them as supplied; repositories must control bundle access, retention and
distribution rather than relying on implicit redaction.

Runner logger API: [VSTest test reporting](https://github.com/microsoft/vstest-docs/blob/main/docs/report.md).
