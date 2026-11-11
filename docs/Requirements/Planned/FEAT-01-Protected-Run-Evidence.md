# FEAT-01 planned: protected run-evidence provenance

## Status

**PLANNED** for hosted run provenance. Local real-runner fixtures verify
pass/fail, skip and rerun behavior for MSTest, xUnit v2/v3 and NUnit under
the active FEAT-01-003 requirement. No protected hosted test artifacts
or required-check evidence has been observed.

## Capability

Collect test-run evidence with protected CI provenance without presenting
untrusted local TRX or source-only inventory as a protected result.

## Requirements

### FEAT-01-004 - Protected test-run provenance

Hosted release and consumer gates MUST record the exact CI run and trusted
artifact identity for supplied TRX files, verify their integrity, and
distinguish protected required-check results from local fixtures.

**Security:** an uploaded or edited TRX file must not be presented as
protected evidence solely because its internal test metadata is valid.
