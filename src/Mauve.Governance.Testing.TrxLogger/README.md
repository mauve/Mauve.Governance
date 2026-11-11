# Governance TRX logger

The stock VSTest TRX logger does not include the test-case traits needed by
`TrxEvidence`. Reference this package in each test project and enable the
VSTest logger explicitly: `dotnet test tests/MyTests.csproj --logger governance-trx`.
The runner discovers the `*.testlogger.dll` assembly in the test output and
writes `governance-<id>.trx` in its results directory. This logger reads
`TestResult.TestCase.Traits` from the runner; it does not read source code or
manufacture missing metadata. Tests without a valid Feature cause evidence
import to fail closed.

Use native `[TestProperty("Feature", "...")]` and
`[TestProperty("Requirement", "...")]` for MSTest, native
`[Trait("Feature", "...")]` / `[Trait("Requirement", "...")]` for xUnit v2/v3,
and native `[Property("Feature", "...")]` / `[Property("Requirement", "...")]`
for NUnit. xUnit v2/v3 and NUnit also provide `[Feature]` / `[Requirement]`
convenience attributes in their adapter packages. `Requirement` may repeat.

For xUnit v3 under the .NET 10 SDK, the normal `dotnet test` VSTest target is
disabled by Microsoft.Testing.Platform. Use the xUnit Visual Studio adapter
with `Microsoft.NET.Test.Sdk` and invoke `dotnet build tests/MyTests.csproj`
then `dotnet vstest tests/MyTests/bin/Debug/net10.0/MyTests.dll
/Logger:governance-trx`. The standalone in-process `dotnet run` runner does
not load VSTest loggers. Run `bash tests/verify-trx-metadata.sh` from this
repository to verify real TRX emission and strict import for all four adapters.

Runner logger API: [VSTest test reporting](https://github.com/microsoft/vstest-docs/blob/main/docs/report.md).
