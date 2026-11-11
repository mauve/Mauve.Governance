# FEAT-02: Managed repository assets and feature documents

## Status

**PARTIAL**. The single .NET tool can bootstrap managed skill/agent/doc
assets, a pinned local tool manifest, copied Markdown files that link to
the canonical policy and warn against direct edits, read-only validation and scheduled
update-PR workflows, reconcile owned files locally, migrate the known
0.2.0-0.9.0 manifests, and validate
the basic active/planned document structure. Hosted consumer validation
requires publishing the pinned tool. The update workflow requires a published
trusted release and configured GitHub App; it has not run in hosted CI.
Explicit adoption and complete profile-driven policy validation remain planned.

## Capability

Give consumers reviewable repo-local assets and preserve ownership when a
bundle is updated, without clobbering consumer-authored files.

## Context

The owning manifest records bundle version, managed paths and content
digests. Asset edits may be replaced only through an explicit local update
and reviewed change. A repository without a workflow cannot self-update.

## Requirements

### FEAT-02-001 - Manifest-owned asset reconciliation

Bootstrap MUST reject unmanaged path collisions; updates MUST replace only
manifest-owned files and MUST reject a tampered manifest. An edited owned
file MAY be replaced in a reviewed update. Enrollment MUST specify the
supported profile and bundle version; a known older manifest MAY migrate
without taking ownership of pre-existing unlisted paths.
Distributed assets MUST originate from one `src/templates/` source tree
embedded by the Tool. The canonical policy MUST be authored there and
installed as a managed copy; feature specifications MUST remain authored
rather than copied. Manifests MUST be stable, two-space-indented JSON.

**Acceptance:** fixture tests cover clean init, no-op update, edited owned
files, unmanaged collisions, pinned tool/workflow assets, a linked managed
policy document whose edits are replaced, known legacy migration,
unsupported profile/version, unknown manifest fields and invalid hashes.

**Security:** a pre-existing consumer file cannot silently become owned by
the framework.

### FEAT-02-002 - Feature-document lifecycle

Repositories using `docs/Requirements/` MUST keep active features separate
from `Planned/` drafts. Active documents MUST have ordered sections and
requirement acceptance/security lines. Planned requirements MUST have a
distinct ID and an explicit PLANNED marker; they are not passing evidence.
The lifecycle validator MUST be available as a reusable library independent
of the CLI. Markdown inside code blocks MUST NOT supply section headings
or required evidence fields.

**Acceptance:** validator tests reject duplicate active/planned IDs, missing
Security lines and missing planned status.

**Security:** unverified designs cannot appear as implemented coverage.

## Metrics Impact

No runtime instruments are added. The tool emits bounded validation counts,
not runtime telemetry.

## Verification

`RepositorySetupTests` checks managed ownership, 0.11.0-to-0.12.0 and earlier
migration, and the `core-requirements` profile's required consumer-owned
lifecycle directory. `Mauve.Governance.Requirements` tests exercise real
Markdown blocks, bounded reading and NativeAOT publication. Packaged tools
validate this repository and the example consumer from a local feed;
Tool-owned release-updater fixtures verify digest rejection, owned-path
staging and retired-script migration. The local tool manifest pins the
version used by both enrolled repositories.
The self-hosted Governance workflow runs this suite and the `validate`
command. The bundled consumer workflows have not been observed in hosted CI;
release publication, update-PR creation and branch protection remain unverified.

## Diagrams

This file-copy and document-validation boundary is direct enough that a
diagram would repeat the manifest-to-managed-path relation without adding
reviewable information.
