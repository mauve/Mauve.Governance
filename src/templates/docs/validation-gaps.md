# Validation gaps and requirement evidence

This editable ledger starts from a Mauve.Governance template; add a row for
**every active requirement** in `docs/Requirements/`, even when all tests pass.
It is not manifest-owned and updates must not overwrite your evidence. See the
[original Mauve.Governance Policy](https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md)
and the installed [policy](../governance.md).

| Requirement | Validation method | Evidence or procedure | Owner | Environment / prerequisites | Gap or waiver details |
| --- | --- | --- | --- | --- | --- |

Use one row per method when a requirement has multiple methods. Classify each
as **missing**, **manual**, **portable automated**, **environment-gated
automated**, or **waived**. Link the test or manual procedure, name gated
prerequisites, and record the actual evidence separately from the intended
method. A waiver needs an approver, owner, reason and expiry; it is not a pass.
a missing or unavailable test is a gap, not an observed success. An
alternative to a C# source test needs a repository-local procedure file
in the Evidence or procedure column. The source declaration gate validates
the ledger but does not merge it with manual or TRX run reports.
For a waiver, record `approver: Name; reason: Explanation; expiry: YYYY-MM-DD`
in Gap or waiver details, and an Owner in its own column. A missing-only
row fails the declaration gate.
