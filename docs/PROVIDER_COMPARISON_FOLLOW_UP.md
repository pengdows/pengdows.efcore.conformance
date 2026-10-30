# Provider comparison follow-up

Updated: 2026-10-30

This is the handoff point for the next work session. The paired profiles, adapters, results, and SQL Server comment fix are committed and pushed. Continue with the black-box reports as the source of truth: keep the shared EF Core expectations unchanged, fix candidate provider behavior where the reference passes, and rerun the same profile pair after each fix.

## Completed in this round

- Added Pengdows and reference profiles for DuckDB, MySQL, and SQL Server. Profiles and launch requirements are in the repository README.
- Ran the same EF Core 10 relational test suite against both sides of each pair.
- Fixed a SQL Server migration comment mismatch in `pengdows.efcore.adonet`: table and column comments now persist as `MS_Description` extended properties.
- Built the conformance solution with zero warnings and errors. The Pengdows SQL Server adapter and provider project also build cleanly.
- SQL Server's clean full run completed without infrastructure errors. Its comment-attribute conformance case passes.
- Commits pushed:
  - `pengdows.efcore.conformance`: `4d7f7b7 Add DuckDB MySQL and SQL Server comparison profiles`
  - `pengdows.efcore.adonet`: `cd44438 Persist SQL Server migration comments`

## Latest pair results

| Engine | Pengdows candidate | Reference | Pass on both | Candidate-only failures | Shared failures | Reference-only failures |
|---|---:|---:|---:|---:|---:|---:|
| DuckDB | 387 pass, 49 fail, 42 unsupported | DuckDB.EFCore 1.0.7: 372 pass, 33 fail, 67 unsupported, 6 skipped | 360 | 12 | 29 | 4 |
| MySQL 8.4 | 662 pass, 46 fail, 30 unsupported | Pomelo: 698 pass, 10 fail, 30 unsupported | 662 | 36 | 10 | 0 |
| SQL Server 2022 | 449 pass, 26 fail, 3 untranslatable | Microsoft: 447 pass, 28 fail, 3 untranslatable | 447 | 0 | 29 (includes 3 untranslatable) | 2 |

DuckDB.EFCore is an independent community provider, not a Microsoft provider. A reference failure is comparison evidence, not proof of an EF Core defect. SQLite's existing Microsoft/Pengdows pair remains matched on outcomes.

## Remaining candidate-only work

### DuckDB

1. Fix `EF10-RAW-SQL-*` candidate-only results (8 sync/async cases). Raw SQL interpolation/parameter execution currently fails because DuckDB receives parameter names such as `p0` as column identifiers. The affected tests cover interpolated and raw commands plus parameterized keyless raw SQL. Keep exercising public APIs; do not inspect generated SQL from shared tests to change their assertions.
2. Fix `EF10-DB-FUNCTION-RANDOM-SYNC/ASYNC`. The candidate translation calls `RAND()`, while DuckDB provides `random()`.
3. Decide and implement candidate support for `EF10-TRANSACTION-ISOLATION-SYNC/ASYNC` with Serializable isolation, or declare a justified database capability gap if DuckDB itself does not support the requested semantics. Do not declare it unsupported solely to remove a failure; check the database contract and the DuckDB.EFCore behavior first.
4. Re-run `pengdows-duckdb.json` and `duckdb-efcore.json`, then regenerate `--compare` output.

### MySQL 8.4

All 36 candidate-only results come from the discovered native type fidelity tests; investigate mapping behavior by type family before changing the adapter mapping profile:

- `BIT` and unsigned `BIGINT` mapped as `UInt64`: round-trip, update, and parameter-query paths throw `InvalidCastException` attempting to cast `UInt64` to `Decimal`.
- Signed `TINYINT` mapped as `SByte`: round-trip, update, null, and parameter-query paths collide with a `Boolean` mapping (`No coercion operator` errors).
- `DECIMAL` mapped as `Decimal`: fractional digits are lost, the full CLR decimal extrema are out of range, and parameterized lookup finds no row. Define and test a suitable precision/scale contract without normalizing away precision loss.
- `GUID` mapped as `Guid` using `char(36)`: round-trip and parameter-query paths throw `InvalidCastException` trying to cast `Guid` to `String`.

Fix the provider or its native mapping behavior so candidate scenarios pass where Pomelo passes. Keep any truly unsupported native type/mapping gap explicit and separate from ordinary EF API conformance. Then rerun the Pengdows and Pomelo profiles and compare all cases.

## Reports and profile setup

The exact reports from this run were written under `/tmp` and may not survive a machine cleanup:

- DuckDB comparison: `/tmp/efcore-conformance-duckdb-comparison.json`
- MySQL comparison: `/tmp/efcore-conformance-mysql-comparison.json`
- SQL Server final candidate: `/tmp/efcore-conformance-sqlserver-pengdows-final-clean/conformance-results.json`
- SQL Server comparison: `/tmp/efcore-conformance-sqlserver-comparison-final.json`

Build candidate adapters explicitly from the conformance repo when the sibling `pengdows.efcore.adonet` checkout is present. Pair profiles and environment variables are documented in `README.md`. Provision an empty SQL Server database before a run so the runner's pre-test ADO.NET type discovery can connect; the lifecycle tests may subsequently delete it. The temporary SQL Server container used in this round was removed.

## Next sequence

1. Fix DuckDB's raw SQL parameter handling and random-function translation; run the affected black-box cases.
2. Resolve Serializable isolation behavior based on DuckDB's documented semantics.
3. Fix the four MySQL native mapping clusters above, keeping fidelity assertions strict.
4. Run all three full candidate/reference pairs and record fresh comparison summaries here and in `README.md` / `llms.txt`.
5. Continue adding provider/server versions and API scenarios; this work expands provider breadth but does not complete the entire EF Core public API behavioral matrix.
