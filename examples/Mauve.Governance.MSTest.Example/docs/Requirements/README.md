# Feature specification lifecycle

This editable guide starts from a Mauve.Governance template. Adapt it to this
repository without weakening the installed
[policy](../../governance.md) or the
[original Mauve.Governance Policy](https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md).
It is not manifest-owned; updates do not replace it.

## Active features

Keep implemented and partially implemented features in
`docs/Requirements/FEAT-<id>-<name>.md`. Each document records Status,
Capability, Context, Requirements, Metrics Impact, Verification and Diagrams.
Each active requirement has a unique `### FEAT-<id>-<requirement>` heading
with explicit **Acceptance:** and **Security:** evidence. Link its validation
method in `docs/validation-gaps.md`; a documented test is not proof it ran.

## Planned work and decisions

Keep proposals and unimplemented requirements in `Planned/` with an explicit
**PLANNED** status. Promote a requirement or whole feature to its active
owner in the same change as implementation, behavioral tests, traceability,
metrics/diagnostics, architecture and the applicable gate. Keep remaining
drafts under `Planned/` without reusing requirement IDs. Record material
decisions in `docs/Architecture/ADR-ZZZ-*.md` and link affected requirements.
Document retirement and supersession rather than presenting planned or
retired work as current evidence.

The Tool validates present lifecycle files and basic active/planned document
structure; it does not validate promotion evidence or test coverage.
