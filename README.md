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

Use a dedicated disposable database. The migration and database lifecycle cases call `EnsureDeleted` and remove the configured database; other cases use unique rows. An adapter may implement `IAdoNetProviderAdapter` to enable public `DbConnection.GetSchema` type discovery. It may also implement `ISupplementalTypeDiscovery` to report types observed through database catalog metadata when the ADO.NET provider omits `DataTypes`. Supplemental discovery is marked `INDEPENDENT` only when it is independent of the EF model. Discovery from tables created by these tests is marked `SELF_DERIVED` and excluded from native-type coverage counts. SQLite does not advertise `DataTypes`; its current-schema type list is self-derived and cannot establish complete database-type coverage.

The runner does not inspect EF provider services or generated SQL. Latest comparable full runs covered 261 cases per adapter:

| Result | pengdows ADO.NET SQLite | Microsoft EF Core SQLite |
|---|---:|---:|
| Passed | 251 | 256 |
| Failed | 10 | 5 |
| Passed on both | 251 | 251 |
| Candidate-only failures | 5 | — |
| Failures also on reference | 5 | 5 |

Four candidate-only failures are the positive and negative near-maximum decimal values on sync and async paths. A fifth is a negative decimal round-trip through `[Precision(18, 4)]`. With the shared model using `HasPrecision(18, 4)`, Microsoft SQLite preserves them; pengdows SQLite reads them back rounded to ±100000000000000. The five failures on both SQLite providers are async `ElementAt(-1)`, sync and async `RightJoin` unmatched rows, and sync and async TPT `SaveChanges` counts. Reproducing on the reference provider means these cases cannot be attributed to the candidate provider alone; it does not by itself prove an EF Core defect. The comparison JSON retains each case and both reasons. Both providers pass the sync and async migration cases, which discover and apply four public migrations, exercise create/add/rename/drop table and column, index, and data operations, roll back to an earlier migration, verify data through fresh contexts, and reapply the latest migration. Both also pass sync and async `UseTransaction` cases that enlist EF Core in an externally created ADO.NET transaction, roll it back, and verify that a fresh context cannot read the rolled back row.

Both SQLite ADO.NET layers omit the `DataTypes` collection. The pengdows supplemental discovery listed 13 declared types from the test schema, but all 13 were marked `SELF_DERIVED`; independent native-type coverage remains unavailable. These counts describe only the two adapters and their local SQLite databases.

Two adapters are included. The Microsoft SQLite reference adapter is part of the standalone solution. The pengdows ADO.NET SQLite adapter is kept outside the solution because it references the separate `pengdows.efcore.adonet` source tree; building the conformance solution does not require that repository. It configures the SQLite dialect, initializes with `EnsureCreatedAsync`, exposes a public ADO.NET connection for type discovery, and implements cleanup with `EnsureDeletedAsync`. Only call cleanup for a dedicated disposable database.

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

The EF 10 relational tests exercise synchronous and asynchronous entity adds and range adds through both `DbSet` and `DbContext`, sync and async context disposal, dependency-injection context factory and scoped context creation, `DbSet` and `DbContext` attach/update/remove entry points and range operations, `DbSet.Local`, `DbSet.Entry`, `DbSet.AsQueryable`, `DbSet.AsAsyncEnumerable`, and `DbSet.GetAsyncEnumerator`, duplicate-key write failures with the failed entity exposed through `DbUpdateException.Entries`, query composition and streaming, async array/set/dictionary materializers, async `ForEachAsync`, sync and async query `Load`, `AsTracking` overrides, terminal query operators and their empty/multiple-result behavior, set operators (`Contains`, `All`, and `LongCount`), aggregates, EF 10 `LeftJoin` and `RightJoin`, single and split relationship queries, explicit collection and reference loading, TPH/TPT/TPC inheritance, complex property persistence, query translation, and sync/async nested updates, primitive collection persistence and element queries, relationships, graph tracking and explicit cascade processing, tracking and direct `EntityEntry.DetectChanges`, shadow-property querying through `EF.Property` and EF 10 `EF.Constant`/`EF.Parameter` predicate evaluation, escaped wildcard matching with `EF.Functions.Like`, parameterized compiled queries through `EF.CompileQuery` and `EF.CompileAsyncQuery`, sync/async save behavior with automatic change detection disabled, context-wide sync/async `QueryTrackingBehavior`, `PropertyValues.SetValues` and snapshot helpers, tracker clearing, `DbSet` and `DbContext` key lookup, current database values for tracked entities, tracked entity reload, property-entry values and modification state, no-tracking identity resolution, sync/async `DbContext` save events and interception, concurrency detection and sync/async conflict resolution, transactions and savepoints including facade commit/rollback and explicit savepoint release, external ADO.NET transactions, database facade provider identification, transaction behavior and savepoint configuration, command-timeout and connection-string configuration, external connection replacement, named query filters, bulk update/delete, interpolated and raw SQL queries and commands (including `ExecuteSql`/`ExecuteSqlAsync` and scalar `SqlQueryRaw<T>`), migration discovery/application/state including table rename and rollback/reapply, unique index enforcement from `[Index]`, built-in function translation from `[DbFunction]`, nullable function result preservation on sync and async paths, nullable function result preservation on sync and async paths, `[PrimaryKey]`/`[Column]`/`[Unicode]` attribute mapping, `[DeleteBehavior(Restrict)]` dependent-delete enforcement, decimal precision attribute round-trips, database deletion/recreation checks, and pending model change detection, common and additional CLR scalar round-trips, boundary values for `DateOnly`, `TimeOnly`, booleans, empty binary values, and converted enums, sync/async store-generated integer keys, and sync/async decimal precision boundaries. Mapping attributes `[PrimaryKey]`, `[Column]`, and `[Unicode]` configure keys and columns verified through fresh contexts; `[DeleteBehavior(Restrict)]` is exercised with a dependent row and rejected database-side deletion. Persistence assertions use fresh contexts. The current inventory records 563 overload entries in tested API families, 162 uncovered member families, and 262 entries awaiting classification. It classifies 4,051 entries as EF-owned with no provider contract and 7,716 provider service or model-configuration entries as not directly applicable, each with a rationale. The remaining uncovered families and unclassified entries still need review and behavioral coverage. The inventory generator reports no unmatched EF Core selectors.

ADO.NET type discovery preserves raw metadata and, when the provider returns CLR `Type` objects, their assembly-qualified identities. The JSON report also summarizes whether `DataTypes` was advertised, duplicate native type names, missing CLR mappings, and CLR type names that could not be resolved. Native type coverage is computed only when the inventory is independent of the EF model. When there is no independent inventory, `NativeTypeCoverage` is empty and the report states whether self-derived supplemental evidence was observed. With an independent inventory, explicit `[NativeTypeCoverage]` declarations link types to behavioral tests: exact native type names are `COVERED`, while different facets within the same family are `PARTIAL`.

## Version baseline

EF Core packages are pinned to 10.0.14, the stable version selected for this baseline. The package version is centralized for consistent inventory and test runs.
