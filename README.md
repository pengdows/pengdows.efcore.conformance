# pengdows.efcore.conformance

Independent black-box conformance testing for relational EF Core 10 providers. Tests call documented EF Core APIs and assert observable behavior. They do not inspect generated SQL, provider internals, or provider service registrations.

## Architecture and status

- Establish EF Core 10/net10.0 project boundaries.
- Define the provider configuration and database lifecycle contract.
- Load adapters by assembly and type name without compile-time provider references.
- Discover ADO.NET `DataTypes` metadata without assuming provider-specific columns.
- Generate an assembly-derived public API inventory. Contract rules classify EF-owned and provider service APIs with rationale; tested API families take precedence, and remaining provider-applicable members stay uncovered.
- Include Microsoft's SQLite provider as a reference run, so candidate-only failures can be separated from behavior that reproduces on a reference provider.

The inventory generator captures public members from `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Abstractions`, and `Microsoft.EntityFrameworkCore.Relational`. It reads disposition rules from `manifests/efcore-10.json` beside the output file; each non-applicable classification includes its reason. Pass the built relational test assembly as a second inventory argument to link `[BehavioralCoverage]` test contracts to API member families. This family-level link does not claim every overload is independently covered. The runner can execute the shared test project and write JSON and JUnit result reports; test logs and TRX details are sanitized for the configured connection string. Strict runs require adapters to declare every known capability as supported or unsupported. A comparison command classifies matching results as passing on both, candidate-only failures, or failures that also occur on the reference provider.

## Run the initial tools

```sh
dotnet run --project src/pengdows.efcore.conformance.Inventory -- ./manifests/efcore-10-api-inventory.json tests/pengdows.efcore.conformance.EF10.Relational/bin/Debug/net10.0/pengdows.efcore.conformance.EF10.Relational.dll
PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING='...' dotnet run --project src/pengdows.efcore.conformance.Runner -- ./My.Adapter.dll My.Adapter.ProviderAdapter
PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING='...' dotnet run --project src/pengdows.efcore.conformance.Runner -- --discover-types ./My.Adapter.dll My.Adapter.ProviderAdapter ./ado-net-types.json ./path/to/test-assembly.dll
PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING='...' dotnet run --project src/pengdows.efcore.conformance.Runner -- --run tests/pengdows.efcore.conformance.EF10.Relational/pengdows.efcore.conformance.EF10.Relational.csproj ./My.Adapter.dll My.Adapter.ProviderAdapter ./conformance-results
```

Connection strings are read from the environment, not command-line arguments. The solution includes the Microsoft SQLite reference adapter and is buildable without the separate pengdows ADO.NET repository:

```sh
dotnet build pengdows.efcore.conformance.slnx
```

## Provider run profiles

The ADO.NET conformance project’s `providers/*.json` pattern is a good fit for keeping per-database run settings out of shared tests. This EF harness uses smaller JSON profiles because provider setup still belongs in a compiled EF adapter: `adapterAssembly` and `adapterType` select that adapter, and `connectionString` can expand `${ENVIRONMENT_VARIABLE}` placeholders. The adapter remains authoritative for EF provider configuration, database initialization, cleanup, and capability declarations. A profile cannot override capabilities or expected results. Adapters may implement `IParameterizedCollectionModeAdapter` to configure EF Core's public relational collection translation mode for shared behavior tests.

Profiles do not start containers or provision remote databases. Point the profile at a database that is already available, using an environment variable for secrets and machine-specific paths. Adapter assembly paths are relative to the profile file unless absolute.

```sh
dotnet build pengdows.efcore.conformance.slnx
export EFCORE_CONFORMANCE_DB_PATH=/tmp/efcore-reference.db
dotnet run --project src/pengdows.efcore.conformance.Runner -- --run-profile \
  profiles/microsoft-sqlite.json \
  tests/pengdows.efcore.conformance.EF10.Relational/pengdows.efcore.conformance.EF10.Relational.csproj \
  /tmp/efcore-reference-report --cleanup-database
```

`profiles/pengdows-sqlite.json` uses the same shape for the candidate adapter. Build that adapter separately when its sibling ADO.NET repository is available. Direct `--run` commands and `PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING` remain supported.

The optional `profiles/pomelo-mysql.json` profile runs the same EF Core 10 test assembly against MySQL 8.4 through Pomelo. Set `EFCORE_CONFORMANCE_MYSQL_HOST`, `EFCORE_CONFORMANCE_MYSQL_PORT`, `EFCORE_CONFORMANCE_MYSQL_DATABASE`, `EFCORE_CONFORMANCE_MYSQL_USER`, and `EFCORE_CONFORMANCE_MYSQL_PASSWORD` before invoking `--run-profile`. Use a dedicated database: database-lifecycle tests delete and recreate it, and `--cleanup-database` deletes it after the run.

Run the conformance tests through the runner to produce comparable reports:

```sh
export PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING='Data Source=/tmp/efcore-reference.db'
dotnet run --project src/pengdows.efcore.conformance.Runner -- --run \
  tests/pengdows.efcore.conformance.EF10.Relational/pengdows.efcore.conformance.EF10.Relational.csproj \
  "$PWD/adapters/pengdows.efcore.conformance.Adapter.MicrosoftSqlite/bin/Debug/net10.0/pengdows.efcore.conformance.Adapter.MicrosoftSqlite.dll" \
  pengdows.efcore.conformance.Adapter.MicrosoftSqlite.MicrosoftSqliteAdapter /tmp/efcore-reference-report --cleanup-database
```

Run the same test project against any other adapter using a separate connection string and report directory. Compare the resulting `conformance-results.json` files with `--compare`. `--cleanup-database` is opt-in and calls the adapter's cleanup hook after the run; use it only with a dedicated disposable database.

Direct `dotnet test tests/pengdows.efcore.conformance.EF10.Relational` runs require these environment variables:

- `PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING`
- `PENGDOWS_EFCORE_ADAPTER_ASSEMBLY`
- `PENGDOWS_EFCORE_ADAPTER_TYPE`

Use a dedicated disposable database. Migration and database-lifecycle cases delete and recreate the configured database; the runner’s optional cleanup deletes it after the run. An adapter may implement `IAdoNetProviderAdapter` to enable public `DbConnection.GetSchema` type discovery. It may also implement `ISupplementalTypeDiscovery` to report types observed through database catalog metadata when the ADO.NET provider omits `DataTypes`. Supplemental discovery is marked `INDEPENDENT` only when it is independent of the EF model. Discovery from tables created by these tests is marked `SELF_DERIVED` and excluded from native-type coverage counts. SQLite does not advertise `DataTypes`; its current-schema type list is self-derived and cannot establish complete database-type coverage.

The runner does not inspect EF provider services or generated SQL. The latest full runs covered 326 cases on each adapter:

| Result | pengdows SQLite | Microsoft SQLite | Pomelo MySQL 8.4 |
|---|---:|---:|---:|
| Cases | 326 | 326 | 326 |
| Passed | 306 | 311 | 313 |
| Failed | 10 | 5 | 5 |
| Unsupported capabilities | 10 | 10 | 8 |

Comparing Pomelo MySQL with Microsoft SQLite, 305 cases pass on both; the same five cases fail on both: async negative `ElementAt`, sync and async unmatched-row `RightJoin`, and sync and async TPT `SaveChanges` affected-entry counts. Six MySQL unsupported outcomes are the sync and async filtered-index, JSON-mapping, and primitive-collection cases. SQLite declares ambient `System.Transactions` enlistment unsupported; those two cases pass on MySQL. `Least`/`Greatest` translation is explicitly unsupported by all three adapters. Comparing pengdows SQLite with Microsoft SQLite, 306 cases pass on both, five fail only on pengdows SQLite, five fail on both, and ten are unsupported by both SQLite adapters (ambient transactions, `Least`/`Greatest`, SQLite migration rename/check-constraint, add-constraint migration, index-schema metadata, and comment metadata). The five candidate-only failures are decimal fidelity cases. Both SQLite adapters pass the public `CreateDbCommand` query execution case, all six context-wide parameterized collection mode cases, sync and async `EF.MultipleParameters(collection)` queries, and both `EF.Functions.Random()` cases, which assert a result strictly between zero and one. Both SQLite adapters pass the sync and async `SaveChanges(false)` cases that verify state retention and subsequent `AcceptAllChanges`. Matching failures on a reference provider are evidence for triage, not automatic proof of an EF Core defect. The MySQL adapter also verifies a named `[Index]` through its public ADO.NET `GetSchema("Indexes")` metadata; both SQLite adapters declare that metadata collection unavailable. MySQL also verifies table and column `[Comment]` annotations through public ADO.NET table and column metadata; SQLite does not expose those comment fields through its schema collections.

Four candidate-only failures are the positive and negative near-maximum decimal values on sync and async paths. A fifth is a negative decimal round-trip through `[Precision(18, 4)]`. The explicit decimal-to-string value-conversion cases preserve all declared boundary values and work in sync and async predicates on both SQLite adapters. With the shared model using `HasPrecision(18, 4)`, Microsoft SQLite preserves them; pengdows SQLite reads them back rounded to ±100000000000000. The parameterized collection mode matrix covers `Constant`, `Parameter`, and `MultipleParameters` on sync and async paths; all six cases and explicit `EF.MultipleParameters(collection)` calls pass on both SQLite adapters. The five failures on both SQLite providers are async `ElementAt(-1)`, sync and async `RightJoin` unmatched rows, and sync and async TPT `SaveChanges` counts. Reproducing on the reference provider means these cases cannot be attributed to the candidate provider alone; it does not by itself prove an EF Core defect. The comparison JSON retains each case and both reasons. Both providers pass the sync and async migration cases, which discover eight public migrations, apply five portable migrations across all providers, and capability-gate index rename, check-constraint, and add-key-constraint operations. MySQL additionally verifies added primary/unique/foreign-key constraints, cascade deletion, and rollback. The shared cases exercise table and column changes, indexes, data operations, and raw SQL migrations, then verify rollback and reapplication through fresh contexts. Both SQLite adapters pass sync and async `UseTransaction` cases that enlist EF Core in an externally created ADO.NET transaction, roll it back, and verify that a fresh context cannot read the rolled back row. MySQL passes sync and async ambient `TransactionScope` enlistment cases and verifies committed rows from fresh contexts.

Both SQLite ADO.NET layers omit the `DataTypes` collection. The pengdows supplemental discovery listed 13 declared types from the test schema, but all 13 were marked `SELF_DERIVED`; independent native-type coverage remains unavailable for SQLite. MySQL Connector advertises `DataTypes` and reported 51 metadata rows representing 41 distinct native names, with no unresolved CLR type names or discovery diagnostics. Coverage is tracked per distinct mapping variant, not just type name: the 51 metadata rows contain 49 variants, including signed/unsigned and GUID-format mappings. Current declarations cover 4 variants, partially cover 1, leave 44 uncovered, and include 8 test type names not observed by MySQL. These uncovered results are real gaps in native-type round-trip coverage; the inventory discovery is independent, but the behavioral type cases are not generated from it yet.

The standalone solution includes Microsoft SQLite and Pomelo MySQL adapters. The pengdows ADO.NET SQLite adapter is kept outside the solution because it references the separate `pengdows.efcore.adonet` source tree; building the conformance solution does not require that repository. The MySQL adapter configures Pomelo for MySQL 8.4 and uses MySqlConnector for public ADO.NET metadata discovery. Its declared unsupported capabilities are filtered indexes, JSON mapping, and primitive collections.

```sh
dotnet build pengdows.efcore.conformance.slnx
export PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING='Data Source=/tmp/efcore-conformance.db'
dotnet run --project src/pengdows.efcore.conformance.Runner -- --run \
  tests/pengdows.efcore.conformance.EF10.Relational/pengdows.efcore.conformance.EF10.Relational.csproj \
  "$PWD/adapters/pengdows.efcore.conformance.Adapter.PengdowsSqlite/bin/Debug/net10.0/pengdows.efcore.conformance.Adapter.PengdowsSqlite.dll" \
  pengdows.efcore.conformance.Adapter.PengdowsSqlite.PengdowsSqliteAdapter /tmp/efcore-candidate-report --cleanup-database
```

Building the pengdows candidate adapter requires a checkout of `pengdows.efcore.adonet` at the sibling path referenced by its project file. The adapter is outside the standalone solution build.

## Current behavioral slice

The EF 10 relational tests exercise synchronous and asynchronous entity adds and range adds through both `DbSet` and `DbContext`, sync and async context disposal, dependency-injection context factory and scoped context creation, `DbSet` and `DbContext` attach/update/remove entry points and range operations, `DbSet.Local`, `DbSet.Entry`, `DbSet.AsQueryable`, `DbSet.AsAsyncEnumerable`, and `DbSet.GetAsyncEnumerator`, duplicate-key write failures with the failed entity exposed through `DbUpdateException.Entries`, query composition and streaming, async array/set/dictionary materializers, async `ForEachAsync`, sync and async query `Load`, `AsTracking` overrides, terminal query operators and their empty/multiple-result behavior, set operators (`Contains`, `All`, and `LongCount`), aggregates, EF 10 `LeftJoin` and `RightJoin`, parameterized collection translation in constant, parameter, and multiple-parameter modes through sync and async query paths, single and split relationship queries, explicit collection and reference loading, TPH/TPT/TPC inheritance, complex property persistence, query translation, and sync/async nested updates, primitive collection persistence and element queries, relationships, graph tracking and explicit cascade processing, tracking and direct `EntityEntry.DetectChanges`, sync/async behavior for all `PropertyAccessMode` values, shadow-property querying through `EF.Property` and EF 10 `EF.Constant`/`EF.Parameter`/`EF.MultipleParameters` collection predicate evaluation, escaped wildcard matching with `EF.Functions.Like`, parameterized compiled queries through `EF.CompileQuery` and `EF.CompileAsyncQuery`, sync/async save behavior with automatic change detection disabled, `SaveChanges(false)` persistence followed by `AcceptAllChanges`, context-wide sync/async `QueryTrackingBehavior`, sync/async snapshot and notification change-tracking strategies, `SaveChanges(false)` state retention followed by `AcceptAllChanges`, and changes with snapshot detection disabled, sync/async `WarningBehavior` handling for `Log`, `Ignore`, and `Throw`, `PropertyValues.SetValues` and snapshot helpers, tracker clearing, `DbSet` and `DbContext` key lookup, current database values for tracked entities, tracked entity reload, property-entry values and modification state, no-tracking identity resolution, sync/async `DbContext` save events and interception, concurrency detection and sync/async conflict resolution, transactions and savepoints including facade commit/rollback and explicit savepoint release, external ADO.NET transactions, database facade provider identification, transaction behavior and savepoint configuration, command-timeout and connection-string configuration, external connection replacement, named query filters, bulk update/delete, interpolated and raw SQL queries and commands (including `ExecuteSql`/`ExecuteSqlAsync`, scalar `SqlQueryRaw<T>`, and `CreateDbCommand` execution), migration discovery/application/state including table rename, raw SQL migration operations, and rollback/reapply, unique index enforcement from `[Index]`, built-in function translation from `[DbFunction]`, `EF.Functions.Random()` result fidelity on sync and async paths, and capability-gated `EF.Functions.Least`/`Greatest` behavior, nullable function result preservation on sync and async paths, `[PrimaryKey]`/`[Column]`/`[Unicode]` attribute mapping, `[DeleteBehavior(Restrict)]` enforcement plus database cascade and set-null behavior, decimal precision attribute round-trips, database deletion/recreation checks, synchronous and asynchronous database seeding hooks, and pending model change detection, common and additional CLR scalar round-trips, boundary values for `DateOnly`, `TimeOnly`, booleans, empty binary values, and converted enums, sync/async store-generated integer keys, and sync/async decimal precision boundaries and decimal-to-string value-conversion boundary round-trips plus predicates. Mapping attributes `[PrimaryKey]`, `[Column]`, and `[Unicode]` configure keys and columns verified through fresh contexts; `[DeleteBehavior(Restrict)]` is exercised with a dependent row and rejected database-side deletion. Persistence assertions use fresh contexts. The current inventory records 652 overload entries in tested API families, 25 uncovered member families, and 26 entries awaiting classification. It classifies 3,523 entries as EF-owned with no provider contract and 8,347 provider service or model-configuration entries as not directly applicable, each with a rationale. The remaining uncovered families and unclassified entries still need review and behavioral coverage. The inventory generator reports no unmatched EF Core selectors. Public type declarations, compiler-generated enum backing fields, and inherited `System.Object` overrides carry explicit non-applicability rationales because they are inventory nodes or EF-owned object behavior rather than provider operations.

## Unfinished work

This is an active EF Core 10 conformance slice. The counts below describe different inventory dimensions and **must not be added together**: unclassified entries can belong to uncovered member families.

| Work area | Current state | Work required |
|---|---|---|
| Public API inventory | 17,988 entries from Core, Abstractions, and Relational; 652 entries linked to tested member families; 31 member families uncovered | Review every uncovered family. Add behavioral tests for provider contracts, or document a precise EF-owned, capability-gated, or not-applicable rationale. Family links are not proof that every overload or scenario is covered. |
| Classification | 26 entries remain `unclassified` | Resolve each entry; the largest cluster is `MigrationBuilder` (15 entries), followed by migrations option enums and relational capability enums. Do not classify an API as covered unless a test invokes its observable behavior. |
| Failure triage | 5 candidate-only failures and 5 failures reproduced by both SQLite providers | Investigate the five pengdows decimal fidelity failures. Review async negative `ElementAt`, `RightJoin` unmatched rows, and TPT affected-entry counts against documented EF contracts and additional providers before assigning ownership. A reference failure is evidence, not an automatic EF Core defect classification. |
| Native database types | SQLite does not advertise ADO.NET `DataTypes`; its supplemental list is self-derived | Run against providers with independent type catalogs. Compare ADO.NET metadata with the database catalog, record omissions and invalid mappings, then exercise convention, explicit mapping, and converter paths for discovered type families. Cover boundary/null round-trips and applicable query operations. |
| Provider breadth | Comparable execution currently uses Microsoft SQLite, pengdows SQLite, and Pomelo MySQL 8.4 | Run the same suite against additional relational providers and server versions. Keep provider capability declarations explicit and preserve the same shared expectations. |
| Contract evolution | EF Core 10.0.14 is the only executable target | Add EF Core 11 as a separate target after establishing its release contract; regenerate its assembly inventory and add version-specific tests only where APIs or behavior differ. |

### Completion criteria

The EF Core 10 milestone is complete when the inventory has no unclassified entries, every provider-applicable public API family has a behavioral contract or a documented reason it does not apply, and each claimed contract has sync and async coverage where both APIs exist. Native type coverage must be based on independent discovery, failures must be triaged against documented behavior and reference providers, and reports must distinguish pass, failure, unsupported capability, infrastructure error, and uncovered work. The current 326-case three-provider comparison does not meet these criteria.

ADO.NET type discovery preserves raw metadata and, when the provider returns CLR `Type` objects, their assembly-qualified identities. The JSON report also summarizes whether `DataTypes` was advertised, duplicate native type names, missing CLR mappings, and CLR type names that could not be resolved. Native type coverage is computed only when the inventory is independent of the EF model. When there is no independent inventory, `NativeTypeCoverage` is empty and the report states whether self-derived supplemental evidence was observed. With an independent inventory, explicit `[NativeTypeCoverage]` declarations link types to behavioral tests: exact native type names are `COVERED`, while different facets within the same family are `PARTIAL`.

## Version baseline

EF Core packages are pinned to 10.0.14, the stable version selected for this baseline. The package version is centralized for consistent inventory and test runs.
