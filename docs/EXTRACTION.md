# Historical extraction from Mauve.Fleet

Mauve.Fleet's `Requirements/FEAT-11-Repository-Governance.md` informed the
design of this framework. It is **not** the Mauve.Governance Policy, and
installing Governance must not impose Fleet's product-specific choices.
The [policy](governance.md) owns the independent framework contract;
consumer profiles own optional choices and values.

| Fleet requirement | Reusable mechanism | Fleet-owned or consumer-selected choice |
| --- | --- | --- |
| 001 | A canonical policy owner, linked contributor/agent guidance and a CI gate | Fleet's FEAT-11 path, document hierarchy and exact architecture owners |
| 002-003 | File-kind/checker registry, immutable pins, thin workflow validation | BAM globals and build descriptors; checks for languages absent from a consumer |
| 004-005 | Optional feature-document schema, test metadata and traceability | Fleet numbering, `FeatureFact`/`FeatureTheory`-only rule and project ownership |
| 006 | Optional prohibited-interpreter/source-content check | Whether Python is prohibited in a particular repository |
| 007-008 | Reproducible compiled tooling and review/tradeoff templates | Fleet's AOT and publication constraints for individual deliverables |
| 009-011 | Bounded I/O/native-safety checks and performance-evidence schema | Linux interop, boot/OCI/serial paths, baselines and tolerance values |
| 012-013 | Structured logging checks and configurable diagnostic catalog | `MF-*` codes, terminal/ring-buffer implementation and Fleet event IDs |
| 014-015 | Analyzer/style baseline and narrowly scoped remediation guidance | Fleet's SDK pin, platform API bans and BAM/Functions restore rules |
| 016-018 | Suppression inventory and honest evidence/gap/metrics vocabulary | Fleet's pragma exceptions, QEMU/Azure lanes and metric instruments |
| 019-020 | Purpose-oriented naming and configurable dependency graph | Fleet namespaces, Sandbox/Storage graph and platform-root restrictions |

The portable core should enforce only selected, **implemented** rules.
Optional profiles can add feature conventions, strict .NET/AOT requirements,
interpreter bans, performance evidence and consumer product boundaries without
making those constraints universal. Profiles must be versioned, reject unknown
rules, and distinguish configured but unenforced rules from passing checks.
At present only `core` enrollment and limited checks are implemented; the
other profiles and validators in this table are design boundaries, not
shipped features.

Consumer features belong in `docs/Requirements/`, with unimplemented designs
in `docs/Requirements/Planned/`. See the [lifecycle](Requirements/README.md)
for promotion and partial implementation; planned text does not prove test
coverage. Consumers should also adopt NuGet Central Package Management for
reviewable .NET dependency updates. This repository uses both conventions
itself.
