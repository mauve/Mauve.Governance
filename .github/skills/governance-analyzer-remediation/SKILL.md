---
name: governance-analyzer-remediation
description: Fix test metadata diagnostics without weakening governance rules.
---

> **Managed file: do not edit directly.** This skill is copied from
> Mauve.Governance. See the
> [original Mauve.Governance Policy](https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md)
> and put consumer-specific guidance outside managed paths.

Read the [original policy](https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md)
and the owning active feature under `docs/Requirements/`. Copied policy
documents are managed; do not edit them directly. Use repository-specific
documents outside the managed manifest for local decisions.

Fix `MGTEST001` by adding one Feature metadata value to each test method.
Fix `MGTEST002` with a valid `FEAT-` owner. Fix `MGTEST003` by using distinct
requirement IDs that belong to that owner. Fix `MGTEST004` by removing
duplicate or conflicting metadata rather than suppressing the diagnostic.
Choose framework-native `[TestProperty]`, `[Trait]` or `[Property]` forms,
or a verified convenience attribute for the actual test framework. Do not
edit generated output, disable warnings, or claim source metadata proves a
test executed. Rebuild the affected test project and rerun its real metadata
filter before proposing the change.
