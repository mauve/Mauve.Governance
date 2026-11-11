# Feature specification lifecycle

The generic editable starter for this guide is
`src/templates/docs/Requirements/README.md`; this repository retains
additional local guidance. This file is not manifest-owned and updates do
not overwrite it. Follow the installed [policy](../../governance.md) and
the [canonical Mauve.Governance Policy](../governance.md).

Consumer repositories SHOULD keep feature specifications in
`docs/Requirements/` and proposed work in `docs/Requirements/Planned/`.
Both current profiles validate documents in these fixed locations using
the framework's feature-ID grammar. This repository uses the same layout
for its own features.

## Active feature documents

An active `FEAT-<id>-<name>.md` document is the normative owner of the
implemented or partial feature. Use these sections in order:

1. `Status` — distinguish **IMPLEMENTED**, **PARTIAL**, **PLANNED** and
   **PROTECTED/ENVIRONMENT-GATED**; state exact gaps and environments.
2. `Capability` and `Context` — explain boundaries and dependencies.
3. `Requirements` — stable, unique `FEAT-<id>-<requirement>` headings,
   normative behavior, failure/security impact and acceptance conditions.
4. `Metrics Impact` — instruments, units, bounded tags/cardinality, emission,
   privacy/performance, dashboards and tests; explicitly justify no change.
5. `Verification` — link real tests/gates and distinguish portable from
   protected evidence. Source annotations alone are not execution evidence.
6. `Diagrams` — placement/interaction diagrams when they explain a real
   boundary; otherwise give a concrete reason for omission.

Future consumer profiles may tighten the format, but neither current profile
accepts configurable section names, paths or IDs. Do not claim such checks
are enforced before a validator and required CI gate exist.

## Promotion and subsequent changes

`Planned/` proposals are design backlogs. Their headings reserve identifiers
but do not enter active traceability or assert production behavior. Promotion
MUST occur with the change that implements and tests the behavior: move the
owned requirement into the active feature document (or move the whole document
when appropriate), review the CI-generated source traceability, architecture
and capability status, catalog any new diagnostics and metrics, and run
the enabled gates.
Only one document owns a normative requirement ID at a time; an active
document MAY point to remaining drafts in `Planned/`. If verification needs
protected infrastructure, record a durable gap instead of calling it passed.

Later changes MUST update the same owning requirement, tests and evidence.
When a feature is removed or superseded, record the replacement and migration
in an ADR/retirement note and adjust references and gates; do not silently
reuse IDs or present archived requirements as current.

**Current enforcement:** `governance validate` checks required sections,
per-requirement Security/Acceptance lines, matching owning feature headings,
duplicate IDs across active/planned documents and explicit PLANNED markers
when this layout is present. It parses structural Markdown headings (including
setext level-two headings), ignores headings and evidence inside fenced code,
and rejects linked lifecycle files, invalid UTF-8, and feature files over
1 MiB (measured in bytes, including any UTF-8 BOM). Full
profile-driven traceability, metrics fields, evidence and promotion verification
remain planned. The self-hosted CI job runs the implemented structural check.
