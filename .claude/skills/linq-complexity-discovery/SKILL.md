---
name: linq-complexity-discovery
description: Finds LINQ-to-EF queries whose generated SQL is not obvious from the C# (mixed &&/||, nullable columns, TypedId/value converters, hidden joins and subqueries, ExecuteUpdate/ExecuteDelete, predicates duplicated across methods, Join/GroupBy), generates the real SQL on purpose with ToQueryString() and records it above the query. Use whenever you write or change LINQ over a DbSet in ECommerceApp.Infrastructure, and to audit existing repositories (scan / annotate / verify). Requires SQL Server; InMemory cannot show SQL.
---

# LINQ complexity discovery (Claude Code entry point)

The canonical, tool-agnostic definition lives in
[`.github/skills/linq-complexity-discovery/SKILL.md`](../../../.github/skills/linq-complexity-discovery/SKILL.md).
Read that file and follow it; this wrapper exists only so Claude Code lists the skill. Do not copy rules
into this file, they would drift from the canonical one.

Reminders that matter even before you open it:

- The SQL comment is generated on purpose (`ToQueryString()` via a throwaway probe, or EF command logging
  for `ExecuteUpdate`/`ExecuteDelete`), never written from memory. Delete the probe afterwards.
- When you change a query that already has a `// SQL (EF Core, SQL Server):` comment, regenerate it, update
  or delete it, and update the method's description in the same change.
