---
name: linq-complexity-discovery
description: >
  Finds LINQ-to-EF queries whose generated SQL is not obvious from reading the C# (mixed &&/||, nullable
  columns, TypedId/value converters, hidden joins and subqueries, ExecuteUpdate/ExecuteDelete, predicates
  duplicated across methods, Join/GroupBy), then generates the real SQL on purpose and records it above the
  query. Use when writing or changing any LINQ over a DbSet, and to audit existing repositories. Works on
  SQL Server; InMemory cannot show SQL.
argument-hint: "[file path, BC name, or 'scan']"
---

# LINQ complexity discovery

Purpose: make the SQL that a LINQ query becomes visible next to the query, but only where the C# does not
already make it obvious. The SQL comment is generated, never guessed.

## When to use

- Writing or changing a LINQ expression over a `DbSet` / `IQueryable` in `ECommerceApp.Infrastructure`
  (repositories, read services, anything under `Messaging`, `Presale`, `Sales`, `Inventory`, ...).
- Auditing existing queries (mode **scan**).
- Reviewing a diff that touches a query or its comment (mode **verify**).

## Triggers: which queries need a SQL comment

A query needs the comment when **any** of these is true. Counting conditions is not a trigger on its own.

| # | Trigger | Why it hides the real SQL |
|---|---------|---------------------------|
| T1 | `&&` and `||` mixed in one predicate | Precedence and parentheses decide the `WHERE`. |
| T2 | Predicate touches a nullable column (`== null`, `.HasValue`, `<`/`>` on a nullable) | SQL three-valued logic differs from C#: `NULL < x` is not true. |
| T3 | `TypedId`, value converters, enums, `string.Contains/StartsWith`, `DateTime` arithmetic, `.Value` | Translation changes meaning (cast, LIKE, collation) or fails on SQL Server. |
| T4 | `Any` / `All` / `Contains` on a collection, or filters through navigations | Becomes `EXISTS` / `IN` or a hidden join. |
| T5 | The predicate is the correctness guarantee: `ExecuteUpdate`, `ExecuteDelete`, `FromSql`, claim/lease queries | A wrong `WHERE` is a behaviour bug, not a performance bug. |
| T6 | Same predicate written in more than one method | The comment shows they must change together. |
| T7 | `Join`, `GroupJoin`, `GroupBy`, `SelectMany` | The query shape is the SQL. |

Do NOT annotate: a single equality, or plain `AND` between non-nullable columns. If a query had a comment
and a change removed every trigger, delete the comment.

## Modes

### scan

List candidates in the given scope (default: `ECommerceApp.Infrastructure`, excluding `Migrations/`).
Start with a grep over `.Where(`, `.Any(`, `.All(`, `.FirstOrDefaultAsync(`, `.SingleOrDefaultAsync(`,
`ExecuteUpdate`, `ExecuteDelete`, `FromSql`, `.Join(`, `.GroupBy(`, `.SelectMany(`, and multi-line
predicates (lines starting with `&&` / `||`), then read each hit and apply the table above.
Report as a table: `file:line | triggers | has SQL comment? | comment matches generated SQL?`.
Do not edit anything in this mode.

### annotate

For each query that hits a trigger and has no comment:

1. **Generate the SQL on purpose. Do not write it from memory.** Capture the SQL of the REAL repository method
   (no rebuilt expression, so no drift) with command interceptors on a SQL Server-configured context that never
   opens a connection. This works for reads, `ExecuteUpdate` and `ExecuteDelete` alike (verified on EF Core 10):
   ```csharp
   private const string Fake = "Server=tcp:127.0.0.1,1;Database=Probe;User Id=x;Password=x;TrustServerCertificate=True;Connect Timeout=1";
   private sealed class Stop : Exception { }

   private sealed class SuppressOpen : DbConnectionInterceptor   // the connection never opens
   {
       public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r) => InterceptionResult.Suppress();
       public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e, InterceptionResult r, CancellationToken ct = default) => ValueTask.FromResult(InterceptionResult.Suppress());
   }

   private sealed class Capture : DbCommandInterceptor            // record the statement, then stop
   {
       public List<string> Sql { get; } = new();
       private void Record(DbCommand command) { Sql.Add(command.CommandText + Environment.NewLine + "-- params: " + string.Join(", ", command.Parameters.Cast<DbParameter>().Select(p => p.ParameterName + "=" + p.Value))); throw new Stop(); }
       // override ReaderExecuting/ReaderExecutingAsync, NonQueryExecuting/NonQueryExecutingAsync (ExecuteUpdate/Delete)
       // and ScalarExecuting/ScalarExecutingAsync (Count/Any) -> Record(command); return result;
   }

   var capture = new Capture();
   var options = new DbContextOptionsBuilder<OrdersDbContext>().UseSqlServer(Fake).AddInterceptors(capture, new SuppressOpen()).Options;
   // call the real method; outcome: `Stop` thrown -> translated, capture.Sql[0] holds the statement;
   //                        InvalidOperationException "could not be translated" -> translation FAILURE (nothing captured)
   await new OrderRepository(new OrdersDbContext(options)).GetAllAsync(10, 1, "ORD-AB12");
   ```
   - Put the probe in a throwaway test in a project that can see the internal context and repository
     (`ECommerceApp.IntegrationTests` has `InternalsVisibleTo` for Infrastructure), write the captured SQL to a scratch
     file, run it, then **delete the probe**. Never commit a probe.
   - A `could not be translated` result is a real bug on SQL Server that InMemory hides. Stop for that query, report
     it, and do not invent SQL. Add a test in `ECommerceApp.E2E.Backend/Persistence/QueryTranslation/` that runs the
     method on real SQL Server and asserts the results (see `UserProfileQueryTranslationE2ETests`).
   - Pass valid arguments: value-object constructors throw on bad input and that is not a translation error.
   - For an expression that is not inside a repository method, rebuild it and call `ToQueryString()` on the same
     fake-connection context; `ToQueryString()` itself does not exist for `ExecuteUpdate`/`ExecuteDelete`.
   - Parameter names (`@p`, `@now2`, ...) depend on the EF version: leave them as EF produced them.
   - InMemory cannot show SQL. Never use it for this.
2. **Write the comment directly above the query**, stable marker included so it can be found by grep:
   ```csharp
   // SQL (EF Core, SQL Server):
   //   WHERE ([o].[Status] = CAST(0 AS tinyint) AND [o].[NextAttemptAt] <= @now)
   //      OR ([o].[Status] = CAST(1 AS tinyint) AND [o].[LockExpiresAt] < @now)
   // Why: a row is leasable when DUE (Pending, backoff elapsed) or ZOMBIE (Running, lease expired).
   ```
   Keep only the clause that matters (usually `WHERE`/`JOIN`/`GROUP BY`), parameters as `@name`, no
   `DECLARE` noise. SQL says *what*, `Why:` says *why* in one sentence, only if the reason is not obvious.
3. If a method has a business description (like `IOutboxRepository.TryAcquireLeaseAsync`), make sure it
   still matches the predicate.

### verify

Regenerate the SQL for every query that carries a `// SQL (EF Core, SQL Server):` marker and compare it with
the comment. Report drift. Fix drift by updating the comment, or by deleting it when no trigger remains.

## Maintenance rule (DOC-SYNC)

Whoever changes a query that has a SQL comment must, in the same change:
1. regenerate the SQL (same method as above, deliberately),
2. update the comment, or delete it if no trigger remains,
3. update the method's description and any ADR or doc that states the rule the predicate implements
   (grep for the method name and for the marker).

A stale SQL comment is a defect, the same as a stale description.

## Boundaries

- This skill finds and documents complex queries. It does not rewrite them, tune indexes or judge performance.
- It does not run on InMemory test hosts: their provider does not produce SQL.
- Hand-written SQL in comments without the `(hand-written, not generated)` label is not allowed.

## Related

- `.github/instructions/efcore.instructions.md` (the short rule that points here)
- `docs/adr/0010/outbox-claim-and-inmemory-test-decorator.md` (example: the lease predicate, T1 + T2 + T5 + T6)
