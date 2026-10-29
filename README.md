# pengdows.efcore.conformance

Independent black-box conformance testing for relational EF Core 10 providers. Tests call documented EF Core APIs and assert observable behavior. They do not inspect generated SQL, provider internals, or provider service registrations.

## Architecture and status

- Establish EF Core 10/net10.0 project boundaries.
- Define the provider configuration and database lifecycle contract.
- Load adapters by assembly and type name without compile-time provider references.
- Discover ADO.NET `DataTypes` metadata without assuming provider-specific columns.
- Generate an assembly-derived public API inventory whose entries still require applicability and behavioral coverage review.
- Include Microsoft's SQLite provider as a reference run, so candidate-only failures can be separated from behavior that reproduces on a reference provider.

The inventory generator captures public members from `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Abstractions`, and `Microsoft.EntityFrameworkCore.Relational`. Pass the built relational test assembly as a second inventory argument to link `[BehavioralCoverage]` test contracts to API member families. This family-level link does not claim every overload is independently covered. The runner can execute the shared test project and write JSON and JUnit result reports; test logs and TRX details are sanitized for the configured connection string. Strict runs require adapters to declare every known capability as supported or unsupported. A comparison command classifies matching results as passing on both, candidate-only failures, or failures that also occur on the reference provider.

## Run the initial tools

```sh
dotnet run --project src/pengdows.efcore.conformance.Inventory -- ./efcore-10-api-inventory.json tests/pengdows.efcore.conformance.EF10.Relational/bin/Debug/net10.0/pengdows.efcore.conformance.EF10.Relational.dll
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

Use a dedicated disposable database. The current tests insert unique rows and do not delete the database or its contents. An adapter may implement `IAdoNetProviderAdapter` to enable public `DbConnection.GetSchema` type discovery. It may also implement `ISupplementalTypeDiscovery` to report types observed through database catalog metadata when the ADO.NET provider omits `DataTypes`. Supplemental discovery is marked `INDEPENDENT` only when it is independent of the EF model. Discovery from tables created by these tests is marked `SELF_DERIVED` and excluded from native-type coverage counts. SQLite does not advertise `DataTypes`; its current-schema type list is self-derived and cannot establish complete database-type coverage.

The runner does not inspect EF provider services or generated SQL. Latest comparable full runs covered 171 cases per adapter:

| Result | pengdows ADO.NET SQLite | Microsoft EF Core SQLite |
|---|---:|---:|
| Passed | 162 | 166 |
| Failed | 9 | 5 |
| Passed on both | 162 | 162 |
| Candidate-only failures | 4 | — |
| Failures also on reference | 5 | 5 |

The four candidate-only failures are the positive and negative near-maximum decimal values on sync and async paths. With the shared model using `HasPrecision(18, 4)`, Microsoft SQLite preserves them; pengdows SQLite reads them back rounded to ±100000000000000. The five failures on both SQLite providers are async `ElementAt(-1)`, sync and async `RightJoin` unmatched rows, and sync and async TPT `SaveChanges` counts. Reproducing on the reference provider means these cases cannot be attributed to the candidate provider alone; it does not by itself prove an EF Core defect. The comparison JSON retains each case and both reasons. Both providers pass the sync and async migration cases, which discover and apply a public migration, check applied and pending migration state, execute a parameterized insert, and verify persistence through a fresh context. Both also pass sync and async `UseTransaction` cases that enlist EF Core in an externally created ADO.NET transaction, roll it back, and verify that a fresh context cannot read the rolled back row.

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

The EF 10 relational tests exercise sync and async insert and range insert, update and range update, delete and range delete, duplicate-key write failures, query composition and streaming, terminal query operators and their empty/multiple-result behavior, set operators (`Contains`, `All`, and `LongCount`), aggregates, EF 10 `LeftJoin` and `RightJoin`, single and split relationship queries, explicit collection and reference loading, TPH/TPT/TPC inheritance, complex property persistence and query translation, primitive collection persistence and element queries, relationships, tracking, explicit change detection, tracker clearing, `DbSet` and `DbContext` key lookup, tracked entity reload, property-entry values and modification state, no-tracking identity resolution, save interception, concurrency, transactions and savepoints, external ADO.NET transactions, database facade command-timeout and connection-string configuration, external connection replacement, named query filters, bulk update/delete, interpolated and raw SQL queries and commands (including `ExecuteSql`/`ExecuteSqlAsync` and scalar `SqlQueryRaw<T>`), migration discovery/application/state and pending model change detection, common and additional CLR scalar round-trips, and sync/async decimal precision boundaries. Persistence assertions use fresh contexts. The current inventory records 241 overload entries in tested API families and 9,001 uncovered member families; many entries still need applicability classification and behavioral cases. The inventory generator reports no unmatched EF Core selectors.

ADO.NET type discovery preserves raw metadata and, when the provider returns CLR `Type` objects, their assembly-qualified identities. The JSON report also summarizes whether `DataTypes` was advertised, duplicate native type names, missing CLR mappings, and CLR type names that could not be resolved. Native type coverage is computed only when the inventory is independent of the EF model. When there is no independent inventory, `NativeTypeCoverage` is empty and the report states whether self-derived supplemental evidence was observed. With an independent inventory, explicit `[NativeTypeCoverage]` declarations link types to behavioral tests: exact native type names are `COVERED`, while different facets within the same family are `PARTIAL`.

## Version baseline

EF Core packages are pinned to 10.0.14, the stable version selected for this baseline. The package version is centralized for consistent inventory and test runs.
