# FEAT-03: Test metadata and source-only traceability

## Status

**PARTIAL**. Framework-specific test annotations and their analyzer,
source-only traceability and a VSTest metadata-bearing TRX logger are
implemented. Real runner TRX from all four adapters is verified locally.
Both consumer profiles enforce source/ledger declaration validation through
`validate`. Local-only consumers execute it locally; feed-dependent hosted
consumer jobs require explicit opt-in. Observed protected evidence remains
separate work.

## Capability

Identify one owning feature and matching requirement IDs on test methods
without replacing native test discovery, and enumerate declared tests in a
deterministic source-only index without pretending they ran.

## Context

MSTest, xUnit v2, xUnit v3 and NUnit expose different attribute APIs. Their
native annotations and verified convenience forms share one metadata
vocabulary. A static index is a planning/traceability aid, not run evidence.

## Requirements

### FEAT-03-001 - Portable test metadata analysis

Native and supported convenience annotations MUST identify one owning
feature and zero or more distinct, matching requirement IDs per test
method. Missing, invalid, duplicate and cross-feature metadata MUST be
diagnosed when the analyzer is enabled.

**Acceptance:** analyzer fixtures cover all four frameworks, valid native
and convenience attributes, parameterized tests, and negative cases.

**Security:** source metadata MUST NOT be interpreted as successful
execution, and suppression of a failing diagnostic is not a repair.

### FEAT-03-002 - Deterministic source-only index

The generator MUST read source without loading assemblies, emit a stable
versioned JSON index and fail on ambiguous source metadata. The index
MUST distinguish static case descriptors from actual discovered or
executed cases. Both consumer profiles MUST generate the index in enabled CI or the
local-only validation procedure
without requiring a committed copy. The required declaration gate MUST
compare active requirement IDs with the editable ledger and source links,
requiring a validated repository-local alternative or approved waiver
when a requirement has no C# source link. Missing-only methods cannot
satisfy the gate.

**Acceptance:** focused tests cover native and convenience forms,
parameterized/dynamic descriptors, duplicate identities, invalid
metadata, deterministic output, bounded source traversal, active versus
planned requirement inventory, missing declarations, alternative
procedures and expired waivers. CI uploads the index and a PR
base/head declaration delta as source-only review artifacts.

**Security:** a source-only index MUST NOT fill metadata omitted by a TRX
runner or claim that an unobserved scenario passed.

### FEAT-03-003 - Metadata-bearing runner output

MSTest, xUnit v2/v3 and NUnit test runs MUST produce TRX containing
runner-provided Feature and Requirement metadata for native annotations
and convenience attributes where supported. Missing metadata MUST fail
TRX extraction, rather than being supplied from a source index.

**Acceptance:** `verify-trx-metadata.sh` runs real fixtures under all four
frameworks and strictly imports 11 results from four emitted TRX files;
xUnit v3 uses its VSTest adapter instead of the standalone in-process runner.
`verify-trx-outcomes.sh` checks actual MSTest parameterized pass/fail,
skipped outcomes and distinct reruns without making the normal suite fail.

**Security:** the logger reads runner `TestCase.Traits` only, does not load
source to enrich results, and imports fail closed when Feature is absent.

## Metrics Impact

No runtime instruments are added; the generator reports declared test
and feature counts only.

## Verification

`TestMetadataAnalyzerTests`, `TraceabilityGeneratorTests`, logger unit tests
and the real TRX metadata/outcome scripts exercise these rules.
`tests/verify-local-consumer.sh` exercises packaged metadata,
analyzer and logger assemblies, including a real compiler rejection of
missing metadata in an external project. Governance and package CI generate
a source-only index as a run artifact; Governance compares source declarations
between PR base and head. Both consumer profiles
require fresh source and active-requirement/ledger validation; protected hosted
execution proof remains separate.

## Diagrams

Source annotations feed the static index; independently, the runner emits
TRX for `test-results`. Neither output is substituted for the other.
