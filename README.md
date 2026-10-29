# pengdows.efcore.conformance

Independent black-box conformance testing for relational EF Core 10 providers. Tests call documented EF Core APIs and assert observable behavior. They do not inspect generated SQL, provider internals, or provider service registrations.

## First milestone

- Establish EF Core 10/net10.0 project boundaries.
- Define the provider configuration and database lifecycle contract.
- Load adapters by assembly and type name without compile-time provider references.
- Discover ADO.NET `DataTypes` metadata without assuming provider-specific columns.
- Generate an assembly-derived public API inventory whose entries still require applicability and behavioral coverage review.

The inventory generator captures public members from `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Abstractions`, and `Microsoft.EntityFrameworkCore.Relational`. Pass the built relational test assembly as a second inventory argument to link `[BehavioralCoverage]` test contracts to API member families. This family-level link does not claim every overload is independently covered. The runner can execute the shared test project and write JSON and JUnit result reports; test logs and TRX details are sanitized for the configured connection string.

## Run the initial tools

```sh
dotnet run --project src/pengdows.efcore.conformance.Inventory -- ./efcore-10-api-inventory.json tests/pengdows.efcore.conformance.EF10.Relational/bin/Debug/net10.0/pengdows.efcore.conformance.EF10.Relational.dll
PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING='...' dotnet run --project src/pengdows.efcore.conformance.Runner -- ./My.Adapter.dll My.Adapter.ProviderAdapter
PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING='...' dotnet run --project src/pengdows.efcore.conformance.Runner -- --discover-types ./My.Adapter.dll My.Adapter.ProviderAdapter ./ado-net-types.json
PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING='...' dotnet run --project src/pengdows.efcore.conformance.Runner -- --run tests/pengdows.efcore.conformance.EF10.Relational/pengdows.efcore.conformance.EF10.Relational.csproj ./My.Adapter.dll My.Adapter.ProviderAdapter ./conformance-results
```

Connection strings are read from the environment, not command-line arguments. Tests are run with `dotnet test tests/pengdows.efcore.conformance.EF10.Relational` and require these environment variables:

- `PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING`
- `PENGDOWS_EFCORE_ADAPTER_ASSEMBLY`
- `PENGDOWS_EFCORE_ADAPTER_TYPE`

Use a dedicated disposable database. The current tests insert unique rows and do not delete the database or its contents. An adapter may implement `IAdoNetProviderAdapter` to enable public `DbConnection.GetSchema` type discovery. The runner does not inspect EF provider services or generated SQL. Its latest local run against the included SQLite adapter passed 46 of 46 scenarios; that result applies only to this provider and environment.

An example adapter for `pengdows.efcore.adonet` SQLite is included. It configures the SQLite dialect, initializes with `EnsureCreatedAsync`, exposes a public ADO.NET connection for `DataTypes` discovery, and implements cleanup with `EnsureDeletedAsync`. Only call cleanup for a dedicated disposable database.

```sh
dotnet build pengdows.efcore.conformance.slnx
export PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING='Data Source=/tmp/efcore-conformance.db'
export PENGDOWS_EFCORE_ADAPTER_ASSEMBLY="$PWD/adapters/pengdows.efcore.conformance.Adapter.PengdowsSqlite/bin/Debug/net10.0/pengdows.efcore.conformance.Adapter.PengdowsSqlite.dll"
export PENGDOWS_EFCORE_ADAPTER_TYPE='pengdows.efcore.conformance.Adapter.PengdowsSqlite.PengdowsSqliteAdapter'
dotnet test tests/pengdows.efcore.conformance.EF10.Relational
```

## Current behavioral slice

The EF 10 relational tests exercise sync and async insert, update, delete, duplicate-key write failures, query composition and streaming, aggregates, EF 10 `LeftJoin`, relationships, tracking, concurrency, transactions and savepoints, named query filters, bulk update/delete, raw SQL, and common CLR value round-trips. Persistence assertions use fresh contexts. The current inventory records 120 overload entries in tested API families and 9,080 uncovered member families; many entries still need applicability classification and behavioral cases.

ADO.NET type discovery preserves raw metadata and, when the provider returns CLR `Type` objects, their assembly-qualified identities. The JSON report also summarizes whether `DataTypes` was advertised, duplicate native type names, missing CLR mappings, and CLR type names that could not be resolved. A provider that omits `DataTypes` is reported as incomplete discovery; it is not silently treated as having no native types. Native type-by-type EF mapping and value fidelity scenarios are still outstanding.

## Version baseline

EF Core packages are pinned to 10.0.14, the stable version selected for this baseline. The package version is centralized for consistent inventory and test runs.
