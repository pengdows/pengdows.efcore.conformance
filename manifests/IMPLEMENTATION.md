# EF Core 10 conformance implementation ledger

The machine-readable source ledger is [`implementation-ledger.json`](implementation-ledger.json). The provider validation summary is
[`efcore-10-validation-2026-11-04.json`](efcore-10-validation-2026-11-04.json), with the generated API audit in
[`ef10-contract-audit-2026-11-04.json`](ef10-contract-audit-2026-11-04.json). The versioned contract source is
[`CONFORMANCE.md`](CONFORMANCE.md).

## Current gate

**INCOMPLETE.** The latest gate finds 18,795 upstream API entries, 17,959 explicit exclusions, and 836 applicable entries. Of these, 432 have API-specific selectors attached to executable test methods; 417 overloaded members
remain unverified at signature level. A runner IL scan now links exact EF API method tokens called by attributed test bodies; the remaining gaps are visible in the generated audit. Selector links are traceability evidence, not proof that every behavior is asserted. Two domains (provider configuration and DI/factories) are complete; 20 broad behavior domains remain partial. The controlled SQLite collation mutant also passed: both shared collation contracts pass on Microsoft SQLite and fail on the mutant without being classified unsupported.

## Latest provider runs

| Provider | Cases | Pass | Fail | Unsupported | Errors | Skipped/inconclusive |
|---|---:|---:|---:|---:|---:|---:|
| Microsoft EF Core SQLite 10.0.14 | 492 | 461 | 9 | 22 | 0 | 0 |
| pengdows.efcore.adonet SQLite 1.0.0 | 492 | 466 | 4 | 22 | 0 | 0 |
| DuckDB.EFCore reference 1.0.7 | 610 | 548 | 35 | 21 | 0 | 6 |
| pengdows.efcore.adonet DuckDB 1.0.0 | 610 | 547 | 14 | 49 | 0 | 0 |

The DuckDB candidate's table and column comment tests now execute and fail where comments are not persisted; its `NOCASE` tests pass. Explicit navigation loading, DbSet range, ChangeTracker.Entries, and TrackGraph overload checks pass on all four providers.
The independent DuckDB reference passes comments and collation. Both outcomes are visible as ordinary behavior results rather than being
hidden by unsupported declarations.

## Resume point

1. Add signature-specific tests and oracles for the 417 remaining overloaded API entries; review the 432 API-specific selector links and their assertion bodies.
2. Advance each of the 22 partial behavior domains and test all applicable discovered native types; DuckDB currently has 244 independent
   catalog types, of which 100 have no linked native mapping probe.
3. Extend independent type coverage and finish the remaining behavior domains; the deliberate collation mutant is now a reproducible passing control.

The defect control is recorded in [`defect-detection-control-2026-11-04.json`](defect-detection-control-2026-11-04.json). The suite remains incomplete until the machine gate reports zero applicable API gaps and every behavior domain is complete.
