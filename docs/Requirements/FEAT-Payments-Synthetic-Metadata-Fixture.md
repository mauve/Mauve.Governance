# FEAT-Payments: Synthetic test-metadata fixture

## Status

**IMPLEMENTED (FIXTURE ONLY)**. This identifier tests portable, nonnumeric
feature IDs. There is no payments application or product behavior in this
repository.

## Capability

Provide filterable, runner-observed examples of test metadata for each
supported framework, including parameterized and convenience forms.

## Context

The feature is a synthetic interoperability fixture for FEAT-03. Its
requirement IDs appear in the repository's source-only index and in
real metadata-bearing TRX; they do not describe consumer requirements.

## Requirements

### FEAT-Payments-01 - Native feature and requirement metadata

Each supported runner fixture MUST expose its native Feature and
Requirement metadata to discovery and the governance TRX logger.

**Acceptance:** the real-runner script imports the native fixture under
all four frameworks with this exact requirement ID.

**Security:** no source fallback may compensate for missing runtime traits.

### FEAT-Payments-02 - Parameterized native selection

Each fixture MUST provide a filterable native parameterized test that
keeps this requirement ID in its observed TRX result.

**Acceptance:** filter and TRX scripts assert one selected case and a
matching observed result under the four pinned runners.

**Security:** a discovered case must not be reported as executed when it
does not appear in TRX.

### FEAT-Payments-03 - Convenience metadata feature

Supported xUnit v2/v3 and NUnit convenience attributes MUST preserve the
native Feature and first Requirement traits.

**Acceptance:** the three actual adapters emit this requirement ID for
their convenience fixture result.

**Security:** a convenience wrapper may not invent metadata after a run.

### FEAT-Payments-04 - Repeatable convenience requirements

A convenience fixture MAY associate a second distinct matching requirement
ID with the same test without breaking runner selection.

**Acceptance:** runner filter and real TRX import preserve this second ID
for xUnit v2/v3 and NUnit.

**Security:** duplicate or cross-feature IDs remain invalid test metadata.

## Metrics Impact

No production instruments or payments telemetry are introduced; only
test-run fixture counts are emitted.

## Verification

`tests/verify-metadata-filters.sh` and `tests/verify-trx-metadata.sh`
exercise the actual runners. This is not a consumer feature.

## Diagrams

The fixture attributes-to-runner-traits-to-TRX path is linear and is
documented by the scripts and the FEAT-03 evidence boundary.
