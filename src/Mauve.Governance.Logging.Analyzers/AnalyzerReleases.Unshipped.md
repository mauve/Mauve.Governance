### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
MGLOG001 | Governance | Warning | Missing, zero or nonconstant logging event ID
MGLOG002 | Governance | Warning | Unstable or unverifiable structured logging template

MGLOG001 and MGLOG002 inspect Microsoft.Extensions.Logging.LoggerExtensions
Log/LogInformation/LogWarning/LogError/LogCritical calls, ILogger.Log<TState>,
and LoggerMessage declarations. Opaque custom ILogger wrappers cannot be
validated at their call sites; direct ILogger.Log<TState> calls require a
verifiable template and are flagged when their arbitrary state/formatter
does not provide one.
