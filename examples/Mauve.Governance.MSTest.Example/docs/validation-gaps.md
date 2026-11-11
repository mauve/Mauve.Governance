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
A missing or unavailable test is a gap, not an observed success. The current
Tool does not merge this ledger with manual or TRX reports.
