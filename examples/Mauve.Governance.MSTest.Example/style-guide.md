# Source and documentation style guide

> **Managed file: do not edit directly.** Governance updates replace this
> guide. Propose framework-wide changes to the
> [original Mauve.Governance Policy](https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md);
> put repository-specific style guidance outside managed paths.

This guide describes human-facing source and documentation style, not product
behavior, repository policy, analyzer severities, or validation evidence.
The root `.editorconfig` owns formatting and diagnostic severities; the
[installed policy](governance.md) owns governance obligations. Follow
the applicable repository requirements, architecture decisions, and
contribution instructions if they exist. When this guide conflicts with an
enforced setting or owning requirement, follow the owner and correct the guide.

## MSTY-POL-001 - General style

- Prefer direct, literal code over clever compression.
- Keep types and members focused on one responsibility.
- Make control flow and failure paths easy to scan. Use early returns when
  they reduce nesting without hiding cleanup or state transitions.
- Use established domain terms consistently; do not introduce local synonyms
  merely to shorten a name.
- Keep related code together without hiding ownership in broad utility types.

## MSTY-POL-002 - Formatting and naming

Apply the repository `.editorconfig` and formatter output rather than restating
mechanical formatting rules in prose. Break long argument lists and fluent
chains at semantic boundaries, keep guards close to the inputs they validate,
and avoid unrelated formatting changes in focused patches.

Name values for their role rather than their representation. Include units
where a type does not make them clear. Prefer positive boolean names that read
naturally at call sites, and avoid abbreviations other than established domain
terms. Name asynchronous methods with the `Async` suffix unless an established
interface requires a different name. Follow any repository-specific naming
conventions when they exist.

## MSTY-POL-003 - C# source

- Prefer language features that clarify ownership, nullability, and intent,
  not newer constructs used solely to shorten code.
- Keep public APIs small and purpose-specific; make invalid or ambiguous
  states difficult to express.
- Prefer immutable values for plans, identifiers, and validated configuration.
- Keep platform and protocol details behind narrowly named abstractions.
- Use collection expressions, pattern matching, primary constructors, and
  expression bodies when the configured style permits them and they improve
  readability.
- Avoid unclear boolean parameters; use a named enum or options type when
  the distinction belongs to the domain.

## MSTY-POL-004 - Exceptions and error context

- When throwing, identify the failed operation, affected resource, and
  relevant invalid value or constraint where safe.
- Prefer a specific exception type with typed context when callers must
  inspect or handle a failure; otherwise include useful context in the
  message. Do not create a new type for every message variation.
- Preserve the original exception as the inner exception when translating
  failures across a boundary.
- Keep error context bounded and safe to expose. Never include credentials,
  tokens, or sensitive payloads in exceptions or logs.

Logging and diagnostic-code obligations belong to the
[installed policy](governance.md), not this style guide.

## MSTY-POL-005 - Resource ownership

Make each lifecycle apparent from the code that acquires the resource:

- Dispose resources in the scope that owns them, with `using` or `await using`
  when ownership is lexical.
- Make ownership transfer clear in API names, return types, or concise API
  documentation.
- Keep handles, streams, pooled buffers, registrations, and cancellation
  sources alive only for the operation or object that owns them.
- Pair acquisition and release paths visibly, including failure and
  cancellation. Do not hide ownership in general-purpose helpers or mutable
  global state.

Product-specific lifecycle rules belong in their owning requirements or
architecture decisions rather than this guide.

## MSTY-POL-006 - Comments and documentation

- Comment constraints, invariants, non-obvious tradeoffs, and external
  contracts, not syntax. Keep comments close to the code and remove them
  when the constraint no longer applies.
- Reference an owning requirement or architecture decision when a surprising
  choice follows a durable rule.
- Document public contracts when behavior, ownership, or failure semantics
  are not evident from the signature.
- In Markdown, use short descriptive headings, one idea per paragraph, and
  relative links to files that actually exist in the repository.
- Put normative product behavior and design rationale in their owning
  documents, not a second policy source in comments or general guides.

## MSTY-POL-007 - Tests

- Name tests for the observable behavior and condition under test.
- Keep arrange, act, and assert phases clear without labels when the code
  already communicates them.
- Prefer one behavioral reason for failure per test; multiple assertions
  are appropriate when they describe one result or invariant.
- Use descriptive test data names and keep helpers subordinate to the
  behavior under test.

Test metadata and traceability requirements are governance concerns described
by the [installed policy](governance.md), not style rules.
