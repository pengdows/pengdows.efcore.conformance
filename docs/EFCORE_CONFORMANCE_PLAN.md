# EF Core provider conformance plan

## Purpose and scope

Test EF Core's public relational API by comparing observable results. No SQL baselines, generated SQL assertions, EF internal service inspection, log output, diagnostic events, migration-script text, or performance benchmarks are in scope.

Observable contracts include materialized values, shape, count and explicitly ordered results; persisted state checked over an independent ADO.NET channel; public change-tracker state after `SaveChanges`; exception type/category; and clean query translation failure.

## Verdicts and per-case semantics

Every case records a semantics class: `EfGuaranteed`, `ProviderDefined`, or `Unspecified`.

- **Pass:** output matches the applicable oracle.
- **Hard fail:** an `EfGuaranteed` contract mismatches; a `ProviderDefined` result mismatches the adapter declaration; persisted state or exception contract is wrong; or a supported feature fails.
- **Soft fail:** an `Unspecified` behavior diverges. Record the observed values.
- **N/A:** an unsupported feature fails cleanly under the centralized probe contract.
- **Blocked:** an upstream tier failed, such as schema creation.
- **Harness error:** suite defect, never attributed to a provider.

Comparison shape is an assertion property: unordered queries compare as multisets; ordered queries compare in order. Float tolerances, decimal precision, and temporal precision are explicit per assertion. Collation-dependent cases are `ProviderDefined` and name the adapter's declared collation.

## Capability probe contract

Each capability has a centrally defined probe and expected operation point. Adapter declarations do not override observed behavior.

| Probe outcome | Verdict |
|---|---|
| Setup fails (DDL or configuration rejected) | N/A only if adapter declared unsupported; otherwise hard fail |
| Clean translation failure (`InvalidOperationException` at query compilation) | N/A |
| `NotSupportedException` at execution | N/A if declared unsupported; otherwise hard fail |
| Provider `DbException` at execution | Hard fail: translated database operation was rejected |
| Executes with wrong result | Hard fail, regardless of declaration |
| Executes correctly but capability was undeclared | Pass, with `undeclared support` flag |

A clean translation failure is distinct from a database rejecting translated SQL. Exception messages are never asserted.

## Platform, adapters, and results

Target .NET 10 and EF Core 10.0. The compatibility target is EF Core 10.0; central package management and a lock file pin exact package patches. Each result records EF, provider, ADO.NET driver, and database server versions.

The provider adapter supplies the `Use*()` options hook, `DbProviderFactory`, connection string, database create/drop/reset lifecycle, raw-channel identifier quoting and parameter markers, capability declarations, and native-type mappings. The portable core model is shared. TPT/TPC, complex and JSON models, sequences, computed columns, and migration models are extension contracts owned by the suite; adapters provide their DDL and mapping configuration.

The suite emits a JSON result document per run as the source of truth. xUnit output is a projection of that document; `Assert.Skip` is one possible renderer for N/A or Blocked. The badge site consumes the JSON results, not xUnit output.

## Two-channel verification and oracle

- **Query:** seed through raw ADO.NET, read through EF.
- **Write:** write through EF, verify through raw ADO.NET.
- **Round trip:** exercise EF→EF, EF→ADO.NET, and ADO.NET→EF to expose symmetric encoding/decoding defects.

Default expected results use LINQ-to-Objects over the seed. Null-propagating rewrites or explicit expected values cover SQL null behavior. Tricky semantics (integer division, negative modulo, `IndexOf`, empty aggregates) use literal expected results rather than a shared expression oracle.

## Isolation, tiers, diagnostics

Read-only seeded query cases may run in parallel. Mutation cases own tenant/partition key ranges and clean up afterward. Schema and migration cases use fresh databases per class and run serially.

Run tiers in order: connectivity → schema creation → seed → remaining cases. An upstream failure marks dependent cases Blocked.

The optional ADO.NET boundary observer reports command count, rows read, and rows materialized as diagnostic numbers only. Buffering, split queries, and identity resolution can make these differ. The suite draws no conformance conclusions from these measurements.

## Schema and seed

The portable core covers 32/64-bit integers, decimal precision/scale, varchar/nvarchar, nullable variants, relationships (1:N, 1:1, M:N with/without payload), self-reference, composite and alternate keys, and behaviorally verified constraints. Provider extension contracts cover TPT/TPC, complex/JSON, sequences, computed values, and migrations.

Adversarial seed data includes nulls, empty strings, duplicate sort keys, LIKE metacharacters, quotes, backslashes, trailing spaces, case variation, emoji, surrogate pairs, combining characters, NUL, negative numbers, numeric boundaries, empty groups, and parents with zero/one/many children.

## Test catalog

### Schema, mapping, and persistence

Verify `EnsureCreated`/`EnsureDeleted` idempotence, keys and constraints by behavior, defaults, computed values, identity/sequence/HiLo where supported, schema-qualified tables, table/entity splitting, and FK enforcement. Do not use inconsistent schema catalog APIs as the general oracle.

### Type fidelity

Cover numeric boundaries and overflow; decimal precision/scale; strings including empty/null, Unicode, max length and LOB; DateTime kinds, DateTimeOffset offsets, DateOnly, TimeOnly, TimeSpan and provider precision; binary empty/null/large; Guid across channels; enums, converters, and nullable variants. Provider-declared precision truncation is ProviderDefined. Native types use adapter-supplied DDL and boundary round trips through all channels.

### Query translation

Cover nullable comparisons and booleans; projections; conditional/coalesce; ordering and paging; aggregates and aggregate predicates; GroupBy; joins, correlated subqueries and set operations; Contains/Any/All/Count; string/math/date functions; Include/ThenInclude, filtered include and single/split parity; inheritance; query filters; tracking modes; FromSql composition; compiled queries and streaming.

No-OrderBy results compare as multisets. Null ordering and collation outcomes use the declared semantics class. Paging without ordering is N/A or soft according to the case contract.

### SaveChanges and bulk operations

Cover insert/update/delete batches at size 1, default and maximum; generated keys/defaults/computed values; concurrency token conflicts; graph ordering and cycles; cascade/set-null/restrict; join-table and owned/complex/JSON updates; correct exception category and context usability after errors.

`ExecuteUpdate`/`ExecuteDelete` verify affected counts, raw database state, and unchanged change tracker. EF 10 JSON bulk updates cover complex types mapped with `ToJson()`. Owned-type JSON bulk update has a negative test asserting the documented clean translation failure.

### Transactions, errors, async, migrations

Cover commit/rollback over a second connection; external transaction/connection; savepoints and isolation capabilities; TransactionScope and retry behavior where supported. Error contracts cover constraint, concurrency, translation, cancellation, timeout, and provider `DbException` subtype where applicable.

Every query/save case has sync and async forms where both APIs exist. Cover cancellation before/during enumeration, context pooling reset, independent concurrent contexts, and open/closed external connections.

Migrations run on fresh databases. Verify add/alter/rename/drop and index changes by behavior and data preservation; history correctness; idempotent `Migrate()` and applying idempotent scripts. For a migration set with a later failure, expected transaction behavior is stated per provider and migration operation: assert which earlier operations persist and which history rows exist according to that provider/operation contract. Do not assume a single transaction spans all migrations or that every operation has the same transactional support. Scaffolding is capability-gated and compares model shape and round-trip behavior.

## EF Core 10-specific cases

- .NET 10 `LeftJoin` and `RightJoin`.
- Parameterized collection translation modes: default multiple scalar parameters, single JSON parameter, and constants. Run `Contains` cases in all modes and compare results, without asserting SQL shape. Include empty, singleton, large, null-containing, and parameter-limit lists. Cover EF's padding behavior through result equivalence, including repeated final values.
- Split-query ordering consistency: `AsSplitQuery()` with Include, OrderBy on a non-unique key, and Take. Assert every returned child belongs to a returned parent and children match the single-query result. Semantics: `EfGuaranteed`.
- Complex type value semantics: assignment copies values; LINQ equality compares content. Semantics: `EfGuaranteed`.
- Optional complex types obey EF's required-property constraint; include valid optional and struct complex property cases. A collection of structs is a negative test for the documented unsupported shape.
- `DefaultIfEmpty` regression cases, including child collections where parent information must remain in the result.
- New date translations: `DateOnly.ToDateTime()`, `DateOnly.DayNumber` and subtraction, and microsecond/nanosecond DatePart. Clean translation N/A is allowed for providers that do not translate them; database `DbException` is hard fail.
- Named query filters and selective `IgnoreQueryFilters`.
- Regular non-expression lambda setters for `ExecuteUpdate`.
- JSON and vector features are capability-gated; vector coverage is provider-specific.

## Calibration and badge gates

Before publishing a badge for a suite version, run calibration against SQLite, Npgsql, and SQL Server. Every non-pass must be triaged as a suite bug or a filed provider issue with a link. Review Microsoft.Data.Sqlite DateTime, DateTimeOffset, and UTC fidelity outcomes against its EF 10 breaking-change notes before classification.

Badge levels:

- **Core:** sections schema/mapping, type fidelity, query translation, SaveChanges, and error contracts pass with zero hard fails.
- **Standard:** Core plus bulk operations, transactions, and async/lifecycle.
- **Full:** Standard plus migrations/scaffolding and native-type fidelity.

Soft fails remain visible and do not block badges. Publish the JSON-backed report through the ADO.NET suite's GitHub Pages pipeline.

## Phasing

1. Harness: adapter contract, verdict/semantics data, two-channel verifier, oracle/null handling, tier gating, JSON reporting, xUnit projection.
2. Shared core schema/seed and schema/type cases.
3. Calibration against SQLite, Npgsql, SQL Server; triage every non-pass before trusting verdicts.
4. Query and SaveChanges coverage.
5. Bulk, transaction, async/lifecycle, diagnostic boundary observations.
6. Migrations, native fidelity, and the ADO.NET provider as a target; use fake-provider harness self-tests and real providers for conformance.
7. Badge/report site.
