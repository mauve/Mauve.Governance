# Source-only test traceability

`governance generate` creates a reviewable index of **declared** test-to-feature
and test-to-requirement links. It scans C# files under `--root` without
building projects, loading assemblies or invoking a runner. It recognizes
native MSTest `[TestProperty]`, xUnit `[Trait]` and NUnit `[Property]`
annotations, and the supported Mauve.Governance convenience attributes.

The index records each recognized test's identity, framework, source path,
Feature and Requirement IDs, and any statically identifiable case
descriptors. It groups tests by feature and requirement so a reviewer can
answer, for example, "Which source tests declare that they cover
`FEAT-Checkout-001`?" Source paths make the tests easy to find; **line
numbers are not stored in the JSON**, so adding comments or moving tests
within a file does not make the index stale. Errors still identify their
current source path and line on standard error.

## Generate and review

From the repository root, with the Tool installed:

```sh
governance validate --root .
governance generate --root . --output artifacts/traceability.json
```

`--root .` scans the current directory recursively, excluding build-output
and version-control directories. `--output` names the index file relative to
the current working directory, **not** relative to `--root`. Use an
ignored output path such as `artifacts/traceability.json`; the source-only
report is generated in CI and is not committed.
The angle brackets in `--output <index.json>` are placeholders, not literal
shell arguments. The command replaces the JSON atomically when generation
succeeds; it fails on invalid or ambiguous test annotations instead of
publishing a partial index.

For example, given an xUnit test:

```csharp
using Xunit;

class CheckoutTests
{
    [Fact]
    [Trait("Feature", "FEAT-Checkout")]
    [Trait("Requirement", "FEAT-Checkout-001")]
    public void RejectsExpiredToken() { }
}
```

the JSON lists `CheckoutTests.RejectsExpiredToken()` under
`FEAT-Checkout` and `FEAT-Checkout-001`, with its source path. A reviewer
can inspect the implementation and the owning requirement; the annotation
by itself does not establish that the test is sufficient or has run.
Parameterized tests may have source case descriptors, but dynamic data
cannot be enumerated here, and case counts are not executed-test counts.

## Review the generated index

After changing test annotations, adding/removing a test, or renaming/moving
one to a different file, regenerate locally to inspect the current mapping:

```sh
governance validate --root .
governance generate --root . --output artifacts/traceability.json
```

CI validates declared methods, generates the source-only JSON, and uploads
it as a run artifact. Pull requests also produce a base/head declaration
delta, so reviewers can inspect changed mappings without committing the
snapshot. Normal CI artifact retention applies; reports are not a permanent
source-history archive. CI must fail on invalid metadata or missing active
requirement methods. The same check is required in both consumer profiles
and in the framework's governance and release workflows.

`--check` remains available for repositories that separately choose to
commit an index:

```sh
governance generate --root . --output path/to/committed-index.json --check
```

`--check` computes the same JSON but **does not write it**; missing or
stale output produces `MG-GOV-0002` and exits nonzero. This repository
does not use a committed index or `--check` in CI. Source line numbers
and trivia are excluded, so comment-only edits do not change the mapping.

## Evidence boundaries

An index shows *which test sources claim* a given requirement; invalid
declarations (missing Feature, duplicate/cross-feature IDs, ambiguous
annotations) fail generation. `governance validate` separately compares
active requirements (not `Planned/`) against the editable
`docs/validation-gaps.md` ledger and source links. An active requirement
without a C# source link needs a validated, repository-local alternative
procedure (manual, gated, or non-C# automated) or a valid approved waiver.
`missing` alone fails the required declaration gate. Neither the index nor
the ledger establishes runner discovery or observed success.

For requirements without a C# source link, alternative ledger methods
need an existing repository-local procedure file. For example:

```md
| FEAT-Checkout-001 | manual | `procedures/checkout.md` | Team |  | Pending run |
| FEAT-Checkout-002 | portable automated | `tests/checkout.sh` | Team | Runner | Pending run |
| FEAT-Checkout-003 | environment-gated automated | `tests/checkout.sh` | Team | Hosted access | Pending run |
| FEAT-Checkout-004 | waived |  | Team |  | approver: Lead; reason: Reviewed exception; expiry: 2030-01-01 |
```

Portable non-C# automation must reference a supported script file, and
waivers expire at the end of their recorded ISO date. These examples are
declarations, not observed outcomes.

The generator uses C# syntax rather than compilation, so it cannot prove
runner discovery, infer xUnit v2 versus v3 at runtime, resolve arbitrary
custom-derived attributes or evaluate dynamic data. It does not know
whether any test ran or passed. To record observed outcomes, run tests
with a metadata-bearing TRX logger and import the results separately:

```sh
governance test-results --trx TestResults/run.trx --output TestResults/evidence.json
```

Missing Feature metadata in TRX is an error; the source index is never
used to guess run metadata or fill in missing evidence. Neither report
alone proves protected-environment success. The
`Mauve.Governance.Traceability` library also exposes the source generator
to .NET consumers without requiring this CLI workflow.
