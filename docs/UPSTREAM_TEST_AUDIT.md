# Upstream test audit: what the reimplemented providers test, and what we should test

Audit of the test suites of the providers pengdows reimplements, against the shared EF Core 10 conformance suite and the pengdows
adonet provider tests. Counts are test attributes (`[Fact]`, `[Theory]`, `[SkippableFact]`) in the checkouts available locally on
2026-10-30. Topic counts are files whose name or body mentions the topic, so they are rough: use them to rank, not to measure.

## Sources and size

| Provider | Source | Local checkout | Test attributes |
|---|---|---|---:|
| Pomelo MySQL (EF Core, MySQL 8.4) | `Pomelo.EntityFrameworkCore.MySql/test` | yes | 873 |
| Oracle MySQL EF Core (`MySql.EntityFrameworkCore`) | `oracle-upstream/EFCore/tests` | yes | 153 (plus `MySQL.Data` 1,497, which is the ADO.NET driver, not EF) |
| Npgsql EF Core (PostgreSQL) | `npgsql-efcore-upstream/test` | yes | 1,766 |
| skuirrels DuckDB EF provider | `efcore/DuckDB.EFCoreProvider/test` | yes | 4,887 |
| Microsoft SQL Server EF Core | EF Core functional and specification tests | **no** (only the compiled `Microsoft.EntityFrameworkCore.Relational.Specification.Tests` package in the NuGet cache) | not counted |
| Microsoft SQLite EF Core | as above | **no** | not counted |
| pengdows conformance (EF10 relational) | `tests/pengdows.efcore.conformance.EF10.Relational` | yes | 446 |
| pengdows adonet providers | `pengdows.efcore.adonet/test` | yes | 873 |

The Microsoft suites are the shared specification that Microsoft's own providers run. Obtaining their source is the first step for
auditing those two providers.

## What the upstream suites are made of

The big upstream suites are mostly EF Core's shared functional tests, run per provider, plus provider-specific translation and
scaffolding tests. Areas they cover that pengdows does not yet:

- **Query translation families** (Npgsql, skuirrels): arithmetic, bitwise, comparison, DateTime, DateOnly, DateTimeOffset, duration,
  math, BigInteger, byte array, enum, and character conversion. Our suite has a few of these through `DatabaseFunctionTests`,
  `StringFidelityTests`, and `TypeFidelityTests`, not the translation families themselves.
- **Shared query shapes** (all four): the Northwind, Gears of War, and Conference Planner query suites, with many-to-many, navigations,
  query splitting, set operators and complex navigations. We have `QueryTests`, `QuerySetOperatorTests` and a few navigation tests.
- **Scaffolding and reverse engineering** (Pomelo: 67 files mention it; skuirrels: `DuckDBCatalogScaffolding`, `DuckDBCodeGeneratorTest`,
  `DuckDBDatabaseModelFactoryTest`; Oracle: `MySQLDatabaseModelFactoryTest`, `MySQLCodeGeneratorTest`). Ours has one scaffolding file and
  no reverse-engineering test.
- **Compiled models** (Pomelo: 63 files; skuirrels and Npgsql have `CompiledModel` tests). Ours has none by name.
- **Shadow properties and keyless or view mappings** (Pomelo 22 and 6, skuirrels 14 and 22). Ours has `ToView`/`ToQuery` coverage in
  some files but no dedicated shadow-property suite.
- **Table and entity splitting** (all three have 15 to 17 files). Ours has the same count of files but several are owned-type tests;
  this needs a direct check.
- **Full-text and fuzzy string matching** (Pomelo and Npgsql). Ours has some coverage of string functions; the search functions are not
  tested as such.
- **Batching**: bulk insert, update and delete batching (skuirrels: `BulkInsertBatchingTests`, `BulkUpdateBatchingTests`,
  `BulkDeleteBatchingTests`). Ours has `BulkOperationTests` and the adonet bulk tests, with no batching-size tests.
- **Connection and execution strategy**: Npgsql's `ExistingConnectionTest` and `ExecutionStrategyTest`, Oracle's `ConnectionTests`.
  The adonet repo has retry and execution-strategy tests, but the conformance suite does not.
- **DataBinding**: one or two files upstream, eight in ours by name, so this is already covered.

Areas where we are at or above upstream: `Find` (ours 55 files by name, upstream 23 to 68), keyless and view mapping, concurrency
detection, savepoints and transactions, and interception.

## Priorities

**P1: cover the shared functional surface every provider runs.** These are the largest gaps and affect every user.
1. Query translation families (arithmetic, comparison, DateTime and DateOnly and DateTimeOffset, math, string functions).
2. Scaffolding and reverse engineering: database model factory, code generator, and the scaffolded model's round trip.
3. Compiled models.
4. Shared query shapes: many-to-many, complex navigations, query splitting.

**P2: provider-specific behaviour that users hit.**
5. Shadow properties.
6. Table and entity splitting, verified directly rather than through owned-type tests.
7. Batching thresholds for bulk insert, update and delete.
8. Full-text and fuzzy string search, on the providers that have them.

**P3: infrastructure and edge cases.**
9. Execution strategy and existing-connection behaviour, from the adonet tests, promoted into the conformance suite.
10. DefaultValues and generated defaults.

## How this maps to the completeness axis

Each item above is a group of upstream test cases, not a single API. When we add a scenario, link it to the manifest entries it
covers (`BehavioralCoverage`), so the completeness output counts it. The manifest currently links 801 scenario rows; the upstream suites
suggest the P1 groups alone would add several hundred.

## Not yet audited

- Microsoft SQL Server and SQLite functional suites: source not available locally.
- Oracle's `EntityFramework` (EF6) tests: out of scope, since pengdows targets EF Core.
