# EF Core 10 relational provider contract inventory

This directory defines the provider conformance contract independently from the scenarios currently implemented. The upstream API inventory is generated from the public EF Core 10.0.14 assemblies and is the authoritative list of API declarations to disposition. The broader behavioral inventory below defines outcomes that cannot be represented by one API declaration (notably datatype fidelity, database lifecycle, isolation, and resource cleanup).

## Contract rules

- A contract ID is stable across test renames and is never inferred from a test name.
- Every upstream API inventory entry must have an API/overload-specific executable test or an explicit reviewed exclusion rationale. A behavior-family link alone is recorded as `FAMILY_LINKED_API_UNVERIFIED` and remains uncovered at API granularity. Unclassified and uncovered entries fail the completion gate.
- Capability declarations apply only when the database engine lacks the behavior. A provider implementation omission does not make a database capability unsupported.
- Reports distinguish `PASS`, `FAIL`, `UNSUPPORTED`, `BLOCKED`, and `NOT_TESTED`. A skip is never a pass.
- A conformance completion claim requires a clean gate, no `NOT_TESTED` applicable contracts, results from at least two independent providers, and a verified defective-behavior control.

## Public behavior domains

The `EF10-*` identifiers are the suite-level behavior contract. Existing test contracts may cover subsets; uncovered items remain visible in the implementation ledger.

| Contract ID | Public behavior | Expected oracle | Test status |
|---|---|---|---|
| EF10-CONFIG-01 | Provider registration and DbContext options configure the selected provider and connection/data source. | Public options and successful context use; invalid setup fails clearly. | COMPLETE |
| EF10-CONFIG-02 | Dependency injection resolves contexts and factories with configured provider options. | Public DI resolution and independent context lifetimes. | COMPLETE |
| EF10-MODEL-01 | Entities, scalar properties, keys, alternate/composite keys, constraints, indexes and defaults map to observable schema and behavior. | Database schema and persisted values. | PARTIAL |
| EF10-MODEL-02 | Foreign keys, relationships, owned/complex types, inheritance, shadow properties and navigation fixup honor model configuration. | Materialized graph and persisted rows. | PARTIAL |
| EF10-MODEL-03 | Converters, comparers, generated values and concurrency tokens preserve configured semantics. | Round-trip equality, generation, and concurrency exception. | PARTIAL |
| EF10-QUERY-01 | Filtering, projection, ordering, pagination, grouping, aggregates, joins, subqueries and set operations produce LINQ-defined results. | Exact result sequence and cardinality. | PARTIAL |
| EF10-QUERY-02 | Navigation expansion, Include, AutoInclude, split queries, tracking and no-tracking produce documented graphs and tracking state. | Result graph and ChangeTracker observations. | PARTIAL |
| EF10-QUERY-03 | Null semantics, parameterization and server/client evaluation boundaries follow documented behavior. | Exact values or documented translation exception. | PARTIAL |
| EF10-QUERY-04 | Raw SQL and asynchronous query operators execute with correct parameter binding, results and cancellation. | Database result, parameter value fidelity and cancellation. | PARTIAL |
| EF10-PERSIST-01 | Insert, update, delete, sync/async SaveChanges and change detection persist changes and report affected entries. | Fresh-context database state and documented return values. | PARTIAL |
| EF10-PERSIST-02 | Cascade behavior, generated values, relationship fixup and optimistic concurrency follow model and EF contracts. | Persisted rows, generated values, graph state, concurrency exception. | PARTIAL |
| EF10-PERSIST-03 | ExecuteUpdate and ExecuteDelete apply set-based modifications through public APIs. | Affected row count and fresh-context state. | PARTIAL |
| EF10-TX-01 | Begin, commit, rollback, savepoints, transaction sharing and isolation behave according to advertised database capability. | Visible committed/rolled-back state and transaction state. | PARTIAL |
| EF10-TX-02 | Cancellation and recoverable connection failures leave transaction/context resources usable or disposed as documented. | Cancellation token, subsequent operations and disposal observations. | PARTIAL |
| EF10-MIG-01 | Schema create/delete and migration application/history track the model and applied migrations. | Public migrator operations and history/schema observations. | PARTIAL |
| EF10-MIG-02 | Migration scripts and provider-specific schema limitations are explicit; idempotent scripts work when supported. | Script execution and resulting schema; explicit capability result. | PARTIAL |
| EF10-TYPE-01 | ADO.NET type metadata and independently discovered database types are reconciled with the declared datatype inventory. | Machine-readable discovered type catalog and reconciliation. | PARTIAL |
| EF10-TYPE-02 | Each applicable native type has schema, bind, insert/read, materialize, null, boundary, precision/scale/length, Unicode, binary and temporal round-trip checks. | Exact value fidelity; silent loss is FAIL. | PARTIAL |
| EF10-TYPE-03 | Unsupported CLR/store mappings fail explicitly rather than silently truncating or coercing values. | Documented mapping error or lossless observable value. | PARTIAL |
| EF10-LIFE-01 | Connection ownership, connection/reader/transaction/context disposal and repeated execution follow public lifetime rules. | Public state/disposal probes and repeatable results. | PARTIAL |
| EF10-LIFE-02 | Exception and cancellation paths release resources; concurrent use restrictions are enforced. | Resource probes and documented exceptions. | PARTIAL |
| EF10-ERROR-01 | Invalid configuration/SQL, unsupported operations, constraint, connection, transaction, materialization and conversion failures are surfaced and cleaned up. | Observable exception category and subsequent resource state. | PARTIAL |

`PARTIAL` and `NOT_TESTED` are deliberate gaps, never completion signals. The machine implementation ledger is `manifests/implementation-ledger.json`; the latest implementation and resume status is summarized in `manifests/IMPLEMENTATION.md`. The executable audit generated by the runner names test methods and IDs that cover each row.

## Upstream provenance

`efcore-10-api-inventory.json` is generated from the EF Core 10.0.14 public reference assemblies listed in its header. The suite-level behavior rows are a reviewed synthesis of the public relational provider contract and Microsoft EF Core functional/specification test categories; `docs/UPSTREAM_TEST_AUDIT.md` records local upstream test audit evidence and known audit limitations. Update provenance and contract rows when upgrading EF Core.
