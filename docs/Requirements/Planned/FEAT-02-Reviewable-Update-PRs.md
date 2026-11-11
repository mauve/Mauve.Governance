# FEAT-02 planned: reviewable governance updates

## Status

**PLANNED**. A consumer-side release watcher and automatic update-PR workflow
are bundled. The self-hosted CI fixture verifies local Git PR creation,
refresh and refusal to replace unmanaged branch edits, but no trusted release has been
published, no GitHub App enrollment has been verified and no hosted update
PR has been observed.

## Capability

After one-time enrollment, each repository can propose upgrades to its
managed governance files without rewriting the default branch or touching
consumer-owned paths.

## Requirements

### FEAT-02-003 - Safe automatic update proposals

A pinned consumer-side scheduled/manual workflow MUST check a trusted,
integrity-verified governance release, reconcile only manifest-owned paths,
run the applicable source checks and open or refresh one reviewable PR. It
MUST NOT execute untrusted PR code with a write token. An unenrolled
repository cannot self-update; it requires explicit one-time bootstrap.
The pinned local Tool MUST perform release discovery, package digest
verification, exact released-Tool installation and local reconciliation
before the workflow acquires its PR-write token. The workflow, not a
repository-copied shell updater, MUST perform GitHub App authorization
and PR creation or refresh.

**Security:** a newer release or malicious PR cannot silently acquire
ownership of unrelated files or modify protected branches. The bundled
workflow restricts its write token to PR creation after validation; hosted
verification of that boundary remains outstanding.
