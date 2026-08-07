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
| `EndpointParser` | `ECommerceApp.API` | `Endpoint`, `Action-[:EXPOSED_BY]->Endpoint` |
| `PageParser` | `ECommerceApp.Web` | `Page`, `Action-[:EXPOSED_BY]->Page` |

`Endpoint`/`Page` must run after `Action` — an `EXPOSED_BY` edge is only emitted
when its target `Action` id already exists.

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
