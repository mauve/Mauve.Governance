# Enroll or update a repository

The [Mauve.Governance Policy](governance.md) is authoritative in this
repository. Consumer-facing policy, skills, agents and
workflows installed by the tool originate from embedded `src/templates/`
assets, including `.editorconfig`, `AGENTS.md`, `governance.md` and
`style-guide.md`. These are **managed copies: do not edit
them directly**. Propose
changes to their templates and the original policy source here; add
consumer-specific rules in separate unmanaged files. The Governance root
itself is enrolled as a consumer: its `governance.md`, `style-guide.md` and
`docs/governance.md` are generated from templates. Its root README and
feature documents remain authored in place. The [profile reference](profiles.md) specifies
what each supported profile does and does not enforce.

The Tool also creates starter `docs/Requirements/README.md`,
`docs/Requirements/Planned/README.md` and `docs/validation-gaps.md`
from templates when missing. Unlike managed files, these are editable
repository documents: the Tool never takes ownership of or overwrites
existing content. The extension drafting guide at `docs/Policies/README.md`
is managed; write individual `POL-*.md` extensions beside it.

| Authored template | Installed here | Ownership |
| --- | --- | --- |
| `src/templates/governance.md` | `governance.md` | Managed consumer policy. |
| `src/templates/docs/governance.md` | `docs/governance.md` | Managed copy of the canonical policy; edit the template, not this copy. |
| `src/templates/style-guide.md` | `style-guide.md` | Managed style guide. |
| `src/templates/docs/Policies/README.md` | `docs/Policies/README.md` | Managed drafting guide; matches its template. |
| `src/templates/docs/Requirements/README.md` | `docs/Requirements/README.md` | Editable starter, expanded here for this repository. |
| `src/templates/docs/Requirements/Planned/README.md` | `docs/Requirements/Planned/README.md` | Editable starter, expanded here for this repository. |
| `src/templates/docs/validation-gaps.md` | `docs/validation-gaps.md` | Editable starter with this repository's requirement rows. |

## Previously ungoverned repository

1. Choose a published, trusted Governance release and install its
   `Mauve.Governance.Tool` package to a temporary tool path. For example:
   `dotnet tool install --tool-path <temporary-path> Mauve.Governance.Tool
   --version 0.12.0`. A package feed and .NET 10 runtime must be available.
2. Preview `<temporary-path>/governance init --root <repository> --profile
   core --bundle-version 0.12.0 --check`, then repeat without `--check`.
   Alternatively, select `--profile core-requirements` if the consumer
   will maintain both `docs/Requirements/README.md` and
   `docs/Requirements/Planned/README.md` as its own files. Create both
   and the `Planned/` directory before running `init`; enrollment
   refuses an incomplete lifecycle without writing managed files.
   A `core` enrollment creates missing lifecycle guides and a validation
   ledger automatically; `core-requirements` still requires its lifecycle
   layout to be prepared before enrollment. The preview exits with status 1
   when it finds changes to apply; this is
   expected for a new repository.
   Unmanaged collisions (including an existing `.editorconfig`, `AGENTS.md`,
   `.config/dotnet-tools.json`, identically named skill or agent, or policy
   document) stop enrollment.
   Choose a different owner/path or explicitly move the existing file;
   `init` does not silently take ownership.
3. Review and commit `.mauve-governance/manifest.json` and all managed
   assets. Restore the pinned local tool with `dotnet tool restore`, then
   use `dotnet tool run governance -- validate --root .`. Propose the
   enrollment as a pull request. Require the `managed-assets` CI check
   once the tool package is available from the approved feed.
4. To receive later automatic update PRs, install a GitHub App on the
   consumer with Contents and Pull requests write permissions. Record
   its ID as Actions variable `GOVERNANCE_APP_ID` and private key as
   Actions secret `GOVERNANCE_APP_PRIVATE_KEY`. The scheduled/manual
   workflow itself is installed during enrollment and needs no write
   token until a validated update is ready to propose.

## Existing Governance installation

Use a trusted newer tool to run `governance update --root <repository>
--check`, then `--apply` in a review branch. The known 0.2.0-0.10.0
ownership manifests migrate to the current bundle; edited *owned* files
are replaced in the proposed diff, while any file absent from the old
manifest remains outside Governance ownership. An unknown schema, an
unsupported version, unexpected owned hashes or an unmanaged path
collision blocks migration. Move or reconcile such files explicitly
before trying again; do not delete consumer material automatically.

An ungoverned repository has no update workflow or ownership manifest.
No central release can reach it by itself: use the one-time `init` path
above. The pinned local Tool's `update-release --root <repository>`
discovers a newer trusted release, checks its SHA-256 asset digests,
installs the exact newer Tool from an isolated feed, reconciles
manifest-owned assets, validates and stages the managed diff. The
installed workflow alone obtains a short-lived GitHub App token and
commits on an update branch before opening or refreshing a reviewable
PR. The Tool's `update --apply` changes local files only; it never opens
a PR. The former `.mauve-governance/update.sh` is retired: migrations
remove it only when a previous manifest records its ownership.

## Feature lifecycle and scope

Keep active features under `docs/Requirements/` and proposed work under
`docs/Requirements/Planned/`, following the
[feature lifecycle](Requirements/README.md). Feature documents are
consumer-authored, never overwritten by template reconciliation. The
reusable `Mauve.Governance.Requirements` library validates the document
structure used by the Tool; a Markdown heading inside a code block is
not a policy section. .NET repositories should adopt Central Package
Management and committed lock files. The current `core` gate checks
managed-file integrity, present feature-document structure and
active-requirement/source/ledger declarations; it is **not** the full
policy or observed evidence gate. For run
evidence use metadata-bearing TRX and `governance test-results`, not the
source-only traceability index.

The copied `.editorconfig` sets the logging and test-metadata analyzer
diagnostics to errors, but the `core` workflow does not install the analyzer
package or build projects. .NET consumers that need enforcement must install
`Mauve.Governance.Logging.Analyzers` in their projects and require a build
in CI. The distributed style guide is advisory; it does not add a formatting
gate.

Publishing the tool package and release assets, hosting the GitHub App,
and protecting the consumer's required checks are external prerequisites;
the repository currently has no verified public release or hosted PR run.
