# kg-codegen

Generates the ECommerceApp knowledge-graph seed (Cypher) by parsing this repo's
source with syntax-only Roslyn — no `MSBuildWorkspace`, no compilation, so it
runs against a checkout that doesn't build.

**The model itself is not documented here.** Node types, relationship
vocabulary, the four-layer pattern, the guardrails and the phase roadmap live in
[`docs/architecture/knowledge-graph-ontology-design.md`](../../../docs/architecture/knowledge-graph-ontology-design.md).
The machine-readable schema is [`tools/kg/seed/ontology.json`](../seed/ontology.json);
every emitted node/edge is validated against it and an unknown label or an
undeclared triple is an `error:`, not a warning.

## Run

```bash
# validate only — parses, validates against the ontology, writes nothing
dotnet run --project tools/kg/kg-codegen/KgCodegen -- --root . --check

# emit a seed file
dotnet run --project tools/kg/kg-codegen/KgCodegen -- --root . --out tools/kg/kg-seed.cypher

dotnet build tools/kg/kg-codegen/KgCodegen.sln --nologo
dotnet test tools/kg/kg-codegen/KgCodegen.Tests/KgCodegen.Tests.csproj --nologo
```

| Flag | Default | Meaning |
| --- | --- | --- |
| `--root` | repo root, derived from `AppContext.BaseDirectory` | Repository root to parse. |
| `--ontology` | `<root>/tools/kg/seed/ontology.json` | Schema to validate the graph against. |
| `--check` | off | Parse + validate, suppress all file writes. |
| `--out` | `<out-dir>/kg-seed.<utc-timestamp>.cypher` | Explicit output path. |
| `--out-dir` | `<root>/tools/kg` | Directory for the timestamped default name. |

Exit code is `1` when the validator reports any `error:`, `0` otherwise —
warnings never fail the run.

## What gets parsed

Parsers run in this order; it is a real dependency chain, not a style choice.

| Parser | Source | Emits |
| --- | --- | --- |
| `SpineCatalog` | hand-authored | `System`, `Host` (`ApiHost`/`WebHost`), `Module` |
| `EntityParser` | `ECommerceApp.Infrastructure` | `Entity` (from `IEntityTypeConfiguration<T>` + `ToTable(...)`) |
| `RepositoryParser` | `ECommerceApp.Domain` | `Repository`, `Entity-[:PERSISTED_BY]->Repository` |
| `ActionParser` | `ECommerceApp.Application` | `Action` (public methods of `*Service.cs`) |
| `MessageParser` | `ECommerceApp.Application` | `Message` (types implementing `IMessage`), `Action-[:PUBLISHES]->Message` |
| `MessageHandlerParser` | `ECommerceApp.Application` | `MessageHandler`, `Message-[:HANDLED_BY]->MessageHandler` |
| `JobParser` | `ECommerceApp.Application` | `Job` (types implementing `IScheduledTask`), `Module-[:CONTAINS]->Job`, `{Action\|MessageHandler}-[:SCHEDULES]->Job`, `Job-[:OPERATES_ON]->Entity`, `Job-[:PUBLISHES]->Message` |
| `QueryParser` | `ECommerceApp.Application` | `Query`, `Action-[:USES]->Query` |
| `QueryHandlerParser` | `ECommerceApp.Infrastructure` | `QueryHandler`, `Module-[:CONTAINS]->QueryHandler`, `Query-[:HANDLED_BY]->QueryHandler` |
| `EndpointParser` | `ECommerceApp.API` | `Endpoint`, `Action-[:EXPOSED_BY]->Endpoint` |
| `PageParser` | `ECommerceApp.Web` | `Page`, `Action-[:EXPOSED_BY]->Page` |
| `ScriptModuleParser` | `ECommerceApp.Web/wwwroot/js` + Razor views | `ScriptModule`, `Host-[:CONTAINS]->ScriptModule`, `ScriptModule-[:DEPENDS_ON]->ScriptModule`, `Page-[:USES]->ScriptModule` and resolvable `Page-[:USES]->Endpoint` |
| `RolePolicyParser` | `ECommerceApp.Application` + `ECommerceApp.API` + `ECommerceApp.Web` | atomic `Role`/`Policy`, `Endpoint/Page-[:GOVERNED_BY]->Role/Policy` |

`Endpoint`/`Page` must run after `Action` — an `EXPOSED_BY` edge is only emitted
when its target `Action` id already exists. `RolePolicy` must run after both
controller parsers because its governance edges target their generated nodes.
Role names are read from `UserPermissions.Roles`; only roles actually reached
by a governance edge become graph nodes.

`Message` must run after `Action` (a `PUBLISHES` edge needs its source `Action`
id to exist) and `MessageHandler` after `Message` (a `HANDLED_BY` edge needs its
source `Message` id). `Message` keys come from `MessageTypeRegistry`, resolved
through that file's `using` aliases so a registration reaches the real type
rather than the alias name. Like `Role`/`Policy`, `Message` is deliberately
un-contained: no `Module-[:CONTAINS]->Message` edge is emitted, because a
message is a contract between modules rather than a member of one.

A message type is matched by an exact `IMessage` base-list entry, so DTOs and
enums that merely live in a `Messages/` folder produce no node. A handler class
is one node however many `IMessageHandler<T>` interfaces it declares, and
`idAware` records whether any of them is `IIdAwareMessageHandler<T>`.

`Job` must run after all four of `Action`, `Message`, `MessageHandler` and
`Repository` — every edge it emits targets or sources one of their nodes, and it
walks `RepositoryParser`'s `PERSISTED_BY` edges backwards to reach the `Entity`
behind each injected `I*Repository` field. Its marker is the `IScheduledTask`
interface and nothing else: the nine implementers split across two folder
conventions and two filename suffixes (`*Job.cs`, `*Task.cs`), so the whole
`Application` tree is scanned rather than a filename glob. `taskName` is read
from the `TaskName` getter — either its string literal or the backing const it
names, resolved by identifier, never derived from the class name (the real
`CurrencyRateSyncTask` reports `"CurrencyDownloader"`).

`triggerMode` is deliberately narrow. `IDeferredJobScheduler.ScheduleAsync` is
the only trigger with a statically findable call site, so exactly the jobs
reached by a resolved `SCHEDULES` edge get `"Deferred"` and every other job gets
`null`. `JobTriggerSource.Scheduled` and `.Manual` are properties of rows in the
runtime `ScheduledJob` table, read by `CronSchedulerService` and
`JobTriggerService` — invisible to a syntax parser, and filled in later by Phase
6's `overrides.yaml`. The parser never defaults to a mode.

## Warnings are the product, not noise

The parsers are convention-dependent by design, so anything they cannot extract
*confidently* becomes a warning instead of a guess. A clean run today still
prints ~209 warnings; that is expected. Do not "fix" a warning by making a
parser guess.

- `Could not confidently extract route for …` — an MVC page using conventional
  routing with no explicit `[Route]`. The node is emitted with `route = null`.
  ~171 of these.
- `Could not resolve action for X.Y: IFooService.Bar` — a controller calls a
  service the parser could not map to a concrete `Action` id. No edge is
  emitted. The suffix `(more than one type declares that name)` means the name
  was ambiguous and was deliberately not picked.
- `<Domain|Application> symbols: Duplicate type name 'T', keeping '…'` — two
  types share a simple name. Harmless for resolution paths that never look that
  name up; the ambiguity guard above is what keeps it from producing a wrong
  edge.

Additional role/policy warnings are emitted when an alias has conflicting
project-local declarations, an authorization expression cannot be resolved, a
governance source id has no matching Endpoint/Page node, or class and method
role sets have an empty intersection. These cases never fabricate a node or
edge. The parser is intentionally attribute-only: imperative
`User.IsInRole(...)` branches in action bodies are a documented coverage gap
and do not produce `GOVERNED_BY` edges.

Message and handler warnings:

- `Message 'X' is not registered in MessageTypeRegistry` — the type implements
  `IMessage` but no `Register(typeof(X), "key")` call names it. The node is
  emitted with `key = null` and keeps its `HANDLED_BY` edges, because the
  handlers genuinely run; the missing registration is reported as a fact about
  the code, not hidden by dropping the node. 6 of these today.
- `Could not resolve message type 'X' in <file>` — a simple name that the
  file's `using` directives do not disambiguate, typically because two
  namespaces declare the same message name and both are imported. No edge is
  emitted; the resolver never picks a winner.
- `Could not resolve handled message 'T' for <handler>` — an
  `IMessageHandler<T>` whose `T` matches no known `Message` node. The handler
  node is still emitted, without the edge.
- `Could not extract published message in <action>` — an `EnqueueAsync` /
  `PublishAsync` argument that is neither a `new` expression nor a local
  variable assigned from one.

Handler-sourced publishes (three `StockReconciliationRequired` enqueues) are
silent by design rather than warned about: a `MessageParser` `PUBLISHES` edge
must start at an `Action`, and the ontology has no
`MessageHandler-[:PUBLISHES]->Message` triple — the edge is not merely
unemitted, it is currently unrepresentable. Adding that triple is a candidate
ontology amendment. Job-sourced publishes are a different case and *are*
emitted: `Job-[:PUBLISHES]->Message` is a declared triple, and `JobParser`
produces the two real ones. Both parsers share `OutboxPublishResolver` for the
"which message does this invocation enqueue" step, so there is one
implementation of that resolution, not two.

Job warnings:

- `Could not statically determine trigger mode for job <job>` — no
  `ScheduleAsync` call site names it, so `triggerMode` is `null`. This says the
  mode is not visible in source, **not** that the job never runs; 5 of these
  today, one per non-deferred job. They are structural and fire on every run.
- `Could not resolve repository interface 'I…Repository' for job <job>` — the
  job injects a repository interface that has no `Repository` node.
  `RepositoryParser` scans `ECommerceApp.Domain` only, and
  `IInboxCleanupRepository`/`IOutboxRepository` are declared under
  `ECommerceApp.Application/Messaging/`. 2 of these today, and they are a real
  modelling gap rather than a parse failure. A job with no repository field at
  all (`CurrencyRateSyncTask`) is a structurally correct empty result and warns
  nothing — the two cases must stay distinguishable.
- `Could not resolve TaskName for job <job>` / `Could not resolve scheduled job
  in <file>` / `Could not index job class name '<name>'` — unreachable against
  the repo today; they exist so a block-bodied getter, an unrecognised
  `ScheduleAsync` argument shape, or two jobs sharing a class name degrade to a
  warning instead of a wrong edge or a crash.

Query warnings use the same warn-don't-fabricate rule:

- `Could not extract query in <action>: <argument>` — an `IModuleClient` send site passed an argument that is neither a `new …Query(…)` nor a local holding one, so no query name could be read; no `USES` edge is emitted. Send sites on any other receiver type are skipped silently rather than warned about.
- `Could not resolve query type 'X' in <file>` — a module-client send site did not resolve to a known `Query`; no `USES` edge is emitted.
- `Could not resolve handled query 'X' for <handler>` — an `IQueryHandler<X, TResult>` does not resolve to a known `Query`; the handler node is still emitted.

`ScriptModuleParser` uses a deliberately narrow RequireJS/AMD convention. A
column-zero `define([...], ...)` is a module declaration; `require([...], ...)`
is only a page usage marker. It scans recursively below `wwwroot/js`, strips a
UTF-8 BOM before matching, considers only the first declaration in each file,
and resolves only dependencies whose module ids were discovered in that tree.
The real repository currently yields 10 modules from 12 JavaScript files, 3
`DEPENDS_ON` edges, and 2 `Page-[:USES]->ScriptModule` edges. An indented or
wrapped declaration, a declaration in a comment/template literal, or a named
multi-`define` bundle can therefore be missed or falsely recognized; adding a
JavaScript AST dependency was not justified for this convention-bound tool.
Expected non-matches such as `config.js`, `site.js`, empty dependency arrays,
and unresolved same-host MVC URLs are silent. Unresolved `/api/` URLs warn;
the real repository has 0 `Page-[:USES]->Endpoint` edges because its literal
client URLs target MVC pages rather than API routes. Those same-host calls
expose an ontology gap: a future decision may add `Page-[:USES]->Page` (or a
generalized page/endpoint usage relation) to both `ontology.json` and
`ontology.cypher`; this parser intentionally emits no undeclared triple.

The parser's zero-yield warning is same-run only (`YieldTracker`); detecting a
previously non-zero parser dropping to zero requires persisted cross-run
counts and remains a follow-up to Guardrail 5.

Action resolution goes `IFooService` → `FooService` (the repo-wide convention)
and falls back to "the single class implementing `IFooService`" for decorators
such as `CachedCatalogNavigationService : ICatalogNavigationService`, which has
no `CatalogNavigationService` class. Both lookups refuse to answer when more
than one type matches.

## Tests

`KgCodegen.Tests` mixes three kinds, all of which must stay:

- **Fixture tests** (`ParserTests.cs`) — synthetic controllers/entities in a
  temp directory, one per parser behaviour, including the negative cases
  (warn-don't-fabricate).
- **Pinned real-graph tests** (`PinnedRealGraphTests.cs`) — hand-verified facts
  about *this* repo (exact `Module` count, `Coupon`'s table, the
  `StorefrontController` `[ApiController]` branch, the decorator edge) plus
  lower-bound floors. Floors move up, never down.
- **Subprocess test** — runs the actual built executable once, to catch
  packaging problems no in-process test can see.
