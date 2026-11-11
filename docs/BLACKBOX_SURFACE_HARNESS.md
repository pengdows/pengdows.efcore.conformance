# Black-box surface harness
For the provider conformance plan, verdict contract, EF 10 cases, and calibration gates, see [EF Core provider conformance plan](EFCORE_CONFORMANCE_PLAN.md).

Purpose: test every public EF Core 10 API that a relational provider can observe, using only public calls and observable output.
This follows the completion criteria in `README.md` and `llms.txt`: every applicable API family has behavioural scenarios, sync and
async where both exist, with overload, boundary, failure and composition cases, and reports separate pass, fail, unsupported,
infrastructure error and uncovered work.

## Rules

- A case is an input and an observable output. Assertions compare results, rows, schema visible through the database, thrown
  exception types, and EF's documented return values. No generated SQL, no internal types, no service registrations.
- A case has one of two oracles. **Declared**: EF documents the answer, so the case asserts it. **Differential**: EF defines no
  single answer, so the case records the output on every provider and a divergence is a finding.
- Sync and async variants run independently. An overload runs as its own case.

## Scope

The unit is a public member from `manifests/efcore-10-api-inventory.json`, including each overload. Dispositions are reviewed, not
trusted: a member is excluded only when no public call can produce observable output (for example a type declaration, an enum
member, a constant, or an obsolete error). Members that are EF-owned but callable, such as model metadata reads on a built model,
are cases.

## Phases

0. **Enumerate.** For every member, record its family, its overloads, its sync/async pair, and the driver it needs (model build,
   query, context, database facade, change tracker, migrations, scaffolding). Output: a case list with a count per family.
1. **Value catalogue.** One generator of inputs per parameter type (int, string, Guid, DateTime and friends, enumerations, the
   entity types of a fixture model). Inputs cover boundaries and null.
2. **Drivers.** One driver per family that performs the EF operation that exercises the member and returns its observable output.
3. **Oracles.** Declared expectations where EF documents them; differential comparison elsewhere.
4. **Report.** Per case: pass, fail, unsupported, infrastructure error, or not run, with the reason. Completeness counts come from
   `--completeness`; correctness comes from `--compare` over the same case IDs; speed is recorded per case.

## Scope rule (2026-10-31)

Only the external surface a user of EF Core calls is in scope. Types in `.Internal.` namespaces, EF utility types, and provider-facing
service interfaces (for example `ITypeMappingSource`) are excluded, because testing them black-box would mean implementing or
reaching into EF's internals. Each exclusion carries its reason in the case list.

## Current verified state (2026-11-04)

The runner has a per-case result classifier and runs the same relational test assembly against two SQLite providers and two DuckDB
providers. The validation snapshot is `manifests/efcore-10-validation-2026-11-04.json`; the source inventory and ledger
are `manifests/CONFORMANCE.md` and `manifests/implementation-ledger.json`.

| Provider | Cases | Pass | Fail | Unsupported | Skipped/inconclusive |
|---|---:|---:|---:|---:|---:|
| Microsoft EF Core SQLite 10.0.14 | 491 | 460 | 9 | 22 | 0 |
| pengdows.efcore.adonet SQLite 1.0.0 | 491 | 465 | 4 | 22 | 0 |
| DuckDB.EFCore 1.0.7 | 609 | 547 | 35 | 21 | 6 |
| pengdows.efcore.adonet DuckDB 1.0.0 | 609 | 546 | 14 | 49 | 0 |

The SQLite comparison found 460 cases passing on both, zero candidate-only failures, four failures also present on the reference provider, and
five reference-only failures. DuckDB's independently queried `duckdb_types()` catalog contributes 244 database types and 118 additional
cases; its native-type mapping still has 100 catalog types without linked mapping probes and two failing native-type cases. DuckDB's
public catalog verifies table and column comments, and `NOCASE` collation now runs as a test instead of being declared unsupported. The
reference provider passes these cases; the candidate passes `NOCASE` but fails table and column comment persistence, which is now exposed
as a provider failure. A public index catalog reader also removed three infrastructure errors in the candidate run.

The contract gate has 18,795 upstream inventory entries. It excludes 17,959 entries with explicit dispositions. Of the remaining 836, 432 have API-specific selectors attached to executable test methods, while 417 overload entries remain unverified at
signature level. An IL scan of attributed test methods links exact EF method tokens where those methods are directly called. The signature-specific checks now include persistence/context overloads, all four sync/async `LoadOptions` overloads for collection and reference navigation loading, and array plus `IEnumerable` overloads for `DbSet` range operations, generic and non-generic `ChangeTracker.Entries`, and both `ChangeTracker.TrackGraph` forms. These last focused checks pass on all four providers. A selector link is
traceability evidence; the gate does not claim it proves every behavior or boundary of that member. All 22 broad behavior domains remain `COVERAGE_PARTIAL`; none is complete.

The suite is **incomplete**. The saved results contain provider failures, unsupported cases and skipped DuckDB cases. A complete
positive-control/defective-provider exercise is also outstanding. The per-provider contract details are available by running:

```sh
dotnet run --project src/pengdows.efcore.conformance.Runner -- --contract-gate \
  manifests/efcore-10-api-inventory.json \
  tests/pengdows.efcore.conformance.EF10.Relational/bin/Debug/net10.0/pengdows.efcore.conformance.EF10.Relational.dll \
  manifests/implementation-ledger.json /tmp/ef10-contract-audit.json \
  /path/to/microsoft-sqlite/conformance-results.json \
  /path/to/pengdows-sqlite/conformance-results.json \
  /path/to/duckdb-reference/conformance-results.json \
  /path/to/duckdb-candidate/conformance-results.json
```

`--contract-gate` inventories method attributes and provider result cases; family-level API references stay visibly unverified. Provider
capabilities are only UNSUPPORTED where the database engine itself lacks the behavior. Provider implementation gaps remain FAIL results.
The DuckDB exception for `foreign-key-cascade` is based on DuckDB's documented foreign-key limitation; its native type catalog and
collation/comment support are obtained through DuckDB's public catalog/API.

## Remaining work

1. Add signature-specific tests and oracles for the 417 overloaded upstream API entries, and review the 432 API-specific selector links against their
   assertions. Retain reviewed exclusions only where an API has no provider-observable contract.
2. Finish every `PARTIAL`/`NOT_TESTED` behavior domain in the ledger, including configuration/DI, query families and overloads,
   transaction/resource cleanup, migration behavior, and error paths.
3. Reconcile all independently discovered database types against the supplied inventory and add schema, binding, null, boundary,
   precision/scale/length, Unicode/binary/temporal, and lossless round-trip probes for every applicable type.
4. Add controlled defective-provider fixtures that prove both detection and classification, then rerun the full framework suite and
   all available provider runs after each coverage tranche.

No item may be removed from the inventory because its implementation is difficult. Record a specific exclusion rationale only when the
public API has no observable provider behavior.
