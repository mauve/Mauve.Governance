---
name: governance-test-evidence
description: Examine test-run evidence from TRX and review feature/requirement verdicts.
---

> **Managed file: do not edit directly.** This skill is copied
> from Mauve.Governance. See the
> [original Mauve.Governance Policy](https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md)
> and keep consumer-specific guidance outside managed paths.

Read the repository's governance documentation and run the pinned
`governance test-results` command against explicitly selected TRX files.
Check each input's provenance. Do not use source annotations to fill
missing metadata in TRX. Report skipped-only tests as NotObserved and
mixed passed/failed evidence as Mixed. Never claim an unavailable
environment-gated scenario passed.