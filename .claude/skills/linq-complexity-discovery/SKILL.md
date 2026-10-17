---
name: linq-complexity-discovery
description: Finds LINQ-to-EF queries whose generated SQL is not obvious from the C# (mixed &&/||, nullable columns, TypedId/value converters, hidden joins and subqueries, ExecuteUpdate/ExecuteDelete, predicates duplicated across methods, Join/GroupBy), generates the real SQL on purpose (command-interceptor capture from the real repository method) and records it above the query. Use whenever you write or change LINQ over a DbSet in ECommerceApp.Infrastructure, and to audit existing repositories (scan / annotate / verify). Requires SQL Server; InMemory cannot show SQL.
---

# LINQ complexity discovery (Claude Code entry point)

The canonical, tool-agnostic definition lives in
[`.github/skills/linq-complexity-discovery/SKILL.md`](../../../.github/skills/linq-complexity-discovery/SKILL.md).
Read that file and follow it; this wrapper exists only so Claude Code lists the skill. Do not copy rules
into this file, they would drift from the canonical one.

Reminders that matter even before you open it:

- The SQL comment is generated on purpose, never written from memory: capture the SQL of the real repository
  method with command interceptors on a SQL Server context that never opens a connection (works for reads and
  for `ExecuteUpdate`/`ExecuteDelete`). Delete the throwaway probe afterwards.
- A `could not be translated` result is a real bug that InMemory hides: stop, report it, and add a test under
  `ECommerceApp.E2E.Backend/Persistence/QueryTranslation/` (real SQL Server).
- When you change a query that already has a `// SQL (EF Core, SQL Server):` comment, regenerate it, update
  or delete it, and update the method's description in the same change.
