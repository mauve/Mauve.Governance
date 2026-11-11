# FEAT-04: Structured logging analysis

## Status

**PARTIAL**. The separately installable `Mauve.Governance.Logging.Analyzers`
package and diagnostic
configuration are implemented locally. The installed `core` gate does
not build consumer projects or validate their diagnostic catalogs.

## Capability

Detect unsupported logging templates and event IDs for
Microsoft.Extensions.Logging at Information, Warning, Error and Critical
without imposing product-specific event namespaces.

## Context

Structured templates preserve queryable fields; a stable numeric event
ID allows logs to be correlated with troubleshooting instructions.
Consumers install the analyzer separately and configure their own
diagnostic catalog.

## Requirements

### FEAT-04-001 - Compile-time logging rules

Supported `ILogger` logging calls and `LoggerMessage` declarations at
Information, Warning, Error and Critical MUST use a stable structured
message template and a nonzero compile-time numeric event ID. Debug
and Trace are outside this rule. Unsupported or unverifiable direct
logging state MUST NOT be reported as compliant.

**Acceptance:** analyzer fixtures check each severity, event-ID overloads,
source-generated declarations, constant and dynamic templates, invalid
IDs and unrelated methods. The installed `.editorconfig` sets the
diagnostics to errors; a consumer must also install the logging analyzer and
require a project build to enforce the rule.

**Security:** reject misleading success from missing/opaque event IDs
or templates. Error and Critical troubleshooting catalogs and the
consumer-wide build gate remain separate obligations; their existence
MUST NOT be inferred from analyzer success.

## Metrics Impact

The analyzer adds compile-time diagnostics, not runtime counters.

## Verification

`StructuredLoggingAnalyzerTests` exercises accepted and rejected calls;
the Governance build compiles and runs the analyzer tests. Consumer-wide
CI and catalog validation remain unverified.
`tests/verify-local-consumer.sh` also compiles an external negative fixture
using the packaged analyzer and actual .NET logging APIs, requiring
`MGLOG001` and `MGLOG002` errors. This verifies package enforcement, not a
consumer diagnostic catalog or a product-wide required build gate.

## Diagrams

Source logging calls and declarations -> Roslyn diagnostics -> optional
consumer build gate. The catalog is maintained independently.
