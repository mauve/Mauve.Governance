---
name: governance-bootstrap
description: Enroll a repository in a supported Mauve.Governance bundle and review managed changes.
---

> **Managed file: do not edit directly.** This skill is copied from
> Mauve.Governance. See the
> [original Mauve.Governance Policy](https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md)
> and put consumer-specific guidance outside managed paths.

Read `governance.md` and the
[original policy](https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md)
before proposing enrollment. The copied governance policy and documents
are managed; do not edit them directly. Put consumer-specific requirements
under `docs/Requirements/`, with drafts in `Planned/`.

Check the repository for pre-existing `.config/dotnet-tools.json`, workflows,
agents, skills and governance documents. Obtain an explicit decision to move
or retain collisions; never overwrite or silently adopt an unmanaged file.
For a clean repository, run the pinned Governance tool with `init --root .
--profile core --bundle-version <supported-version> --check`, then run the
same command without `--check` and inspect the ownership manifest and diff.
Select `core-requirements` instead if the consumer owns and maintains both
`docs/Requirements/README.md` and `docs/Requirements/Planned/README.md`.
An older manifest instead uses `update --root . --check` and `--apply`.

Before proposing a PR, verify `validate --root .` and note its **limited**
scope. The tool package must be published on a trusted feed, and an App with
the documented permissions is needed before scheduled update PRs work.
Do not treat unobserved test runs as passing evidence.
