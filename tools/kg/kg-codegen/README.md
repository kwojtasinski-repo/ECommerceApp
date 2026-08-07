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
| `EndpointParser` | `ECommerceApp.API` | `Endpoint`, `Action-[:EXPOSED_BY]->Endpoint` |
| `PageParser` | `ECommerceApp.Web` | `Page`, `Action-[:EXPOSED_BY]->Page` |
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

## Warnings are the product, not noise

The parsers are convention-dependent by design, so anything they cannot extract
*confidently* becomes a warning instead of a guess. A clean run today still
prints ~196 warnings; that is expected. Do not "fix" a warning by making a
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

Two publish sites are silent by design rather than warned about: handlers
(three `StockReconciliationRequired` enqueues) and `*Job.cs` files. A
`PUBLISHES` edge must start at an `Action`, and the ontology has no
`MessageHandler-[:PUBLISHES]->Message` triple — the edge is not merely
unemitted, it is currently unrepresentable. Adding that triple is a candidate
ontology amendment.

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
