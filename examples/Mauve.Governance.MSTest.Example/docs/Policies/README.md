# Drafting repository-specific policy extensions

> **Managed file: do not edit directly.** Mauve.Governance replaces this
> guide during updates. Propose generic changes to the
> [original Mauve.Governance Policy](https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md);
> put repository-specific rules in separate `POL-*.md` files.

Create `docs/Policies/POL-XXXX-Name-Of-Policy.md` for a local rule or for
annotations of generic rules in the [installed policy](../../governance.md).
Give each extension a stable ID, scope, rationale, owner, explicit behavior,
applicable paths and environments, and how enforcement and evidence can be
checked. To annotate a rule, cite its policy ID (for example
`MGOV-POL-003`) and record local values such as thresholds, diagnostic
namespace, owners and linked tests or gates. Do not silently weaken a
mandatory generic rule.

Distinguish requirements from implementation status: state which checks
actually run in required CI, which need a protected environment, and which
are planned or have an approved, expiring gap. A document does not create
an enforced gate. Link related features under `docs/Requirements/` and
architecture decisions where they exist. The Tool manages this README,
not the extension policies you write beside it.
