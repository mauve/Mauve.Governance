# Validation gaps and requirement evidence

The generic editable starter for this ledger is
`src/templates/docs/validation-gaps.md`. This file is not manifest-owned
and updates must not overwrite its evidence. Include
a row for **every active requirement** in `docs/Requirements/`, even when
all tests pass. See the
[original Mauve.Governance Policy](../src/templates/docs/governance.md) and the installed
[policy](../governance.md). These entries identify declared
methods, not proof that a protected or hosted check passed.

| Requirement | Validation method | Evidence or procedure | Owner | Environment / prerequisites | Gap or waiver details |
| --- | --- | --- | --- | --- | --- |
| FEAT-01-001 | portable automated | `Mauve.Governance.TestResults.Tests` | Governance maintainers | .NET test runner | Link the release run report. |
| FEAT-01-002 | portable automated | `Mauve.Governance.TestResults.Tests` | Governance maintainers | .NET test runner | Link the release run report. |
| FEAT-01-003 | portable automated | `tests/verify-trx-metadata.sh` | Governance maintainers | Four pinned runners | Link the release run report. |
| FEAT-02-001 | portable automated | `RepositorySetupTests`, `tests/verify-release-updater.sh` | Governance maintainers | .NET test runner | Hosted release/PR evidence unavailable. |
| FEAT-02-002 | portable automated | `Mauve.Governance.Requirements.Tests` | Governance maintainers | .NET test runner | Link the release run report. |
| FEAT-03-001 | portable automated | `TestMetadataAnalyzerTests` | Governance maintainers | .NET test runner | Consumer-wide gate not implemented. |
| FEAT-03-002 | portable automated | `Mauve.Governance.Traceability.Tests` | Governance maintainers | .NET test runner | Consumer-wide gate not implemented. |
| FEAT-03-003 | portable automated | `tests/verify-trx-metadata.sh` | Governance maintainers | Four pinned runners | Hosted runner evidence unavailable. |
| FEAT-04-001 | portable automated | `StructuredLoggingAnalyzerTests` | Governance maintainers | .NET test runner | Consumer build/catalog gate not implemented. |
| FEAT-Payments-01 | portable automated | `tests/verify-metadata-filters.sh` | Governance maintainers | Framework fixture runners | Link the release run report. |
| FEAT-Payments-02 | portable automated | `tests/verify-metadata-filters.sh` | Governance maintainers | Framework fixture runners | Link the release run report. |
| FEAT-Payments-03 | portable automated | `tests/verify-metadata-filters.sh` | Governance maintainers | Framework fixture runners | Link the release run report. |
| FEAT-Payments-04 | portable automated | `tests/verify-metadata-filters.sh` | Governance maintainers | Framework fixture runners | Link the release run report. |

Use one row per method when a requirement has multiple methods. Classify
each as **missing**, **manual**, **portable automated**, **environment-gated
automated**, or **waived**. Link the test or manual procedure, name gated
prerequisites, and record actual evidence separately from the intended
method. A waiver needs an approver, owner, reason and expiry; it never
counts as a pass. A missing or unavailable test is a gap, not an observed
success or skip. The source declaration gate validates this ledger but does not merge it
with manual or TRX run reports. Alternatives to a C# source test need a
repository-local procedure file in the Evidence or procedure column.
Waivers record `approver: Name; reason: Explanation; expiry: YYYY-MM-DD`
in Gap or waiver details and an Owner in its own column. A missing-only
row fails the declaration gate.
