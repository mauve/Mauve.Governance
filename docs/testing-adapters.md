# Test metadata adapters

Install **one** of `Mauve.Governance.Testing.MSTest`,
`Mauve.Governance.Testing.XunitV2`,
`Mauve.Governance.Testing.XunitV3`, or
`Mauve.Governance.Testing.NUnit` in each test project. Each package depends on
its matching framework and the common `Mauve.Governance.Testing` vocabulary;
the v2/v3 packages must not be combined in one test project. Install
`Mauve.Governance.Testing.Analyzers` separately to check test metadata at
compile time. Consumers should pin their chosen package and runner versions in
`Directory.Packages.props` using NuGet Central Package Management. The
framework packages expose `Metadata.Feature` and
`Metadata.Requirement` constants. xUnit v2, xUnit v3 and NUnit additionally
offer `[Feature("FEAT-Checkout")]` and repeatable
`[Requirement("FEAT-Checkout-001")]` annotations in their respective adapter
namespaces. They add metadata to ordinary `[Fact]`/`[Theory]` or
`[Test]`/`[TestCase]` methods; they do not replace test-discovery attributes.
The packages include XML documentation with copyable examples on these
constants and convenience attributes.
Native and convenience annotations can coexist, but must not assign the same
feature or requirement twice.

Annotate each test **method** with one owning feature and zero or more
distinct requirements of that feature:

```csharp
// MSTest
[TestMethod]
[TestProperty(Metadata.Feature, "FEAT-Checkout")]
[TestProperty(Metadata.Requirement, "FEAT-Checkout-001")]

// xUnit v2 and v3
[Fact]
[Trait(Metadata.Feature, "FEAT-Checkout")]
[Trait(Metadata.Requirement, "FEAT-Checkout-001")]

// NUnit
[Test]
[Property(Metadata.Feature, "FEAT-Checkout")]
[Property(Metadata.Requirement, "FEAT-Checkout-001")]

// xUnit v2/v3 or NUnit convenience form (import the matching adapter namespace)
[Fact] // use [Test] for NUnit
[Feature("FEAT-Checkout")]
[Requirement("FEAT-Checkout-001")]
```

Use the `Metadata` type from the framework-specific package. MSTest 4.5.1
allows subclassing `TestPropertyAttribute`, but the pinned adapter did not
expose the subclass's metadata to `Requirement` filtering. **Use MSTest's
native `[TestProperty]` only**; MSTest convenience attributes are deliberately
not shipped. xUnit v2's native `TraitAttribute` is sealed, so its wrappers
implement `ITraitAttribute` and use registered `ITraitDiscoverer`s; xUnit v3
and NUnit wrappers derive from their native trait/property attributes.
The analyzer
reports `MGTEST001` (missing feature), `MGTEST002` (invalid feature),
`MGTEST003` (invalid or cross-feature requirement) and `MGTEST004`
(duplicate feature or requirement). Identifiers use ASCII letters/digits and
internal `-`, `_` or `.`; features begin `FEAT-`, and requirement IDs begin
with the **entire owning feature** plus `-` and a nonempty identifier. Neither
namespace nor numeric width is fixed. Inherited framework test attributes and
native property attributes with two string constructor arguments are
recognized, as are the shipped one-argument convenience attributes.
Only method-level metadata is checked; class/assembly metadata is not
combined with method-level values.

Install `Mauve.Governance.Logging.Analyzers` separately for `MGLOG001`
(event ID) and `MGLOG002` (message template) on supported
Microsoft.Extensions.Logging calls at Information, Warning, Error and
Critical. Use a constant structured template and a nonzero constant
numeric event ID; Error and Critical IDs additionally need entries in
the repository's troubleshooting catalog. These checks require the
logging analyzer package to be installed in the relevant projects and a required
build gate. The installed `core` governance workflow runs neither a
consumer build nor a diagnostic-catalog validator.

Verified selections with these fixtures: `dotnet test
tests/Mauve.Governance.Testing.MSTest.Tests --filter
'Requirement=FEAT-Payments-02'` (likewise XunitV2 and NUnit). For convenience
tests use `Requirement=FEAT-Payments-04` with xUnit v2/NUnit, or
`-trait 'Requirement=FEAT-Payments-04'` with xUnit v3. For xUnit v3
use `dotnet run --project tests/Mauve.Governance.Testing.XunitV3.Tests --
-trait 'Requirement=FEAT-Payments-02'`; v3 test projects are executables and
their current Microsoft.Testing.Platform integration does not support the
VSTest-target `dotnet test Mauve.Governance.slnx` on the .NET 10 SDK.
Run `bash tests/verify-metadata-filters.sh` to assert exactly one selected test
per framework; unlike `dotnet test` alone, this fails if a filter matches no
tests.

**Run evidence:** Stock TRX loggers omit `Feature` and `Requirement` from the
pinned runners' `UnitTest` records. The separately packable
`Mauve.Governance.Testing.TrxLogger` emits runner-provided metadata:
`dotnet test <project> --logger governance-trx` for MSTest, xUnit v2 and
NUnit. For xUnit v3, build the project with its VSTest adapter, then use
`dotnet vstest <test-assembly.dll> /Logger:governance-trx`; the standalone
in-process runner does not load VSTest loggers. See the
[logger instructions](../src/Mauve.Governance.Testing.TrxLogger/README.md).
The evidence importer reads explicit Feature/Requirement values only; it
never substitutes source declarations, display names or diagnostics for
missing metadata.

The approved logger contract preserves runner-supplied execution fields,
messages, output, metadata and local attachments without silent truncation.
Flat execution records carry explicit parent/child IDs. All runner
properties are retained with identity, type and value in a typed companion;
well-known standard mappings may additionally appear in TRX fields, while
custom properties are not flattened into unrelated attributes. Every
message appears in TRX `TextMessages` in original order with a textual
`[Category]` prefix; standard `StdOut`, `StdErr` and `DebugTrace` remain
available to viewers.

One permanent `<report>.files/runner-data.xml` attachment per run retains
typed properties, exact message category/nullability/order and raw fields.
XML-invalid code units are preserved exactly in raw companion form such as
base64. Raw data may duplicate standard TRX output and increase bundle
storage; no additional per-execution raw file is used. Local result and run
attachments are copied under unique relocatable
`<report>.files/<scope>/<unique>/<name>` paths. Result records reference
copied payloads and companion data retains attachment metadata. Keep the
`.trx` and all referenced files together. Run attachments are also listed in
`ResultSummary/ResultFiles`. Relative result URIs resolve from the test
assembly directory; relative run URIs resolve from the results directory.
Absolute `file:` URIs are accepted; non-file schemes are rejected without
network access. The final TRX marks successful
finalization but is not an atomic publication guarantee for the multi-file
bundle.

No generic size/count budgets on report bytes, executions, results, runs,
definitions, messages, attachments, traits or properties, no field-length or
XML-depth caps, and no bounded-work or constant-memory claim apply to
logger/importer operation. The logger retains
definitions and compact execution linkage; the importer retains governance
observations and aggregates. Memory scales with those retained structures
and the current runner result. Captured output and attachments may contain
secrets; control access and retention rather than silently redact evidence.

The [real fixture verifier](../tests/verify-trx-metadata.sh) imports 11
results from four actual TRX files. The
[outcome verifier](../tests/verify-trx-outcomes.sh) checks eight real runs
with parameterized passes/failures and skips across all four frameworks.
Never infer run evidence from source names or fill missing metadata from
the static index.
The existing scripts do not alone establish the newly approved
full-fidelity, attachment, streaming or former-budget regression criteria;
record those results only after implementation and execution.
