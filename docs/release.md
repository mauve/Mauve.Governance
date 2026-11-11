# Package, consumer and release readiness

`Directory.Build.props` versions the .NET Tool, libraries, analyzers and
framework adapters together. The current bundle is `0.13.0`. This repository
can build and pack packages locally; a local package is not a published or
trusted release. NuGet publication, hosted consumer checks that need a
published Tool, and App-authorized update PR automation are deferred. Do not
represent those hosted paths as operational or a release as available until
the package feed, release identity and required hosted runs have been
verified.

## Local package consumer check

Run the end-to-end fixture from the repository root:

```sh
bash tests/verify-local-consumer.sh
```

The script determines the current version from MSBuild, packs Governance
packages locally, and creates an external scratch directory with
`mktemp -d` and a scoped cleanup trap. The consumer does not inherit this
repository's build properties or package management; a copy of `global.json`
preserves the SDK pin. It uses a NuGet configuration
that clears inherited feeds and maps all `Mauve.Governance.*` packages only
to the local feed; third-party dependencies may restore from NuGet.org. The
fixture copies the repository's exact `global.json` pin into each consumer,
uses consumer-local `Directory.Build.props` and a root `.editorconfig` to
avoid inheriting repository build settings, and isolates tool installation,
tool-manifest restore, test restore and package caches from the user's normal
tool/cache locations.

It exercises actual fresh C# MSTest projects against both `core` and
`core-requirements`: Tool installation, init preview and apply, validation,
the packaged test metadata analyzer, MSTest metadata adapter and TRX logger,
real metadata-bearing TRX import, source-only traceability generation,
no-op update check/apply and preservation of editable requirements and
ledger files. A separate intentionally invalid project requires missing
metadata and structured-logging analyzer diagnostics. This is synthetic
local package-consumer evidence, not acceptance by an actual product
consumer, a hosted workflow run, package publication, or protected branch
evidence. The first chosen product consumer still requires its own framework,
analyzer, diagnostics, evidence and required-gate review.

The current Tool, Requirements library, MSTest metadata adapter and TRX
logger target `net10.0`; those consumer paths require .NET 10. The two
analyzer packages target `netstandard2.0`. No earlier target framework is
verified by the end-to-end local consumer check. This repository pins .NET
SDK `10.0.404`; local validation uses that SDK and the .NET 10 runtime.

## Using packages from a local feed

For first consumer work before publication, pack the Tool and its
Governance dependencies from the checkout as documented in
[`bootstrap.md`](bootstrap.md). Configure NuGet package-source mapping so
`Mauve.Governance.*` can resolve only from the local feed, then install the
exact bundle version from that feed. Third-party dependencies still require
an available package source. NuGet may use cached packages without applying
source mapping; set `NUGET_PACKAGES` to a fresh directory or configure a new
`globalPackagesFolder` in the same NuGet config. Never rely on a same-version
package that may exist in the user's global cache.

For a selected .NET 10 consumer, reference `Mauve.Governance.Testing.MSTest`
and `Mauve.Governance.Testing.Analyzers` in its test project, plus
`Mauve.Governance.Testing.TrxLogger` for metadata-bearing TRX. Add
`Mauve.Governance.Logging.Analyzers` where structured logging diagnostics
are wanted. Run a real `dotnet test --logger governance-trx` and import the
emitted TRX using `governance test-results`. Run `governance generate`
separately for source-only declaration traceability. The generated index
never substitutes for executed test metadata and outcomes.

## Hosted publication and updates (deferred)

Repository workflow definitions are not proof that publishing or hosted
updates have run. Before enabling hosted NuGet-dependent jobs, publish the
exact Tool and required packages to an approved feed and verify restore using
the intended source mapping. Any release should identify its packages and
artifacts unambiguously and record provenance and security review; those
release-evidence controls are not claimed as complete here.

The external policy job is disabled unless the Actions variable
`GOVERNANCE_HOSTED_ENABLED` is exactly `true`. Update PR automation is
disabled unless `GOVERNANCE_RELEASE_UPDATES_ENABLED` is exactly `true`.
Neither opt-in substitutes for the published packages and other prerequisites.
GitHub App update PR automation is also deferred. It requires a trusted
published release with verifiable package
digests, a consumer-installed App with Contents and Pull requests write
permissions, and a successful hosted update review. Do not create or
advertise scheduled updates as working before those prerequisites and a
real hosted run are observed. Local `update --check` and `update --apply`
are local reconciliation only; neither opens a PR.

`governance validate` checks the current managed-file integrity and present
feature-document/source/ledger declarations. It is not complete policy
validation, an analyzer build gate, an observed test-results import, or a
release-readiness verdict. The editable validation ledger and automated
TRX report remain separate evidence inputs; a future release-time merger is
not supplied by the Tool's `test-results` command.
