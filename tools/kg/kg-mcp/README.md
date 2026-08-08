# `kg-mcp`

`kg-mcp` is the read-only, stdio MCP facade over the Neo4j graph produced by `kg-codegen`. It exposes exactly the ten Tier-1 structural queries defined in [the ontology design](../../../docs/architecture/knowledge-graph-ontology-design.md). The model itself is not documented here; the design document is the source of truth for the ontology and tool semantics.

Per-tool parameters, return shapes, error envelopes, the reason each published count is what it is, and how the test suites divide the work live in [`docs/reference/kg-mcp-tools.md`](../../../docs/reference/kg-mcp-tools.md). Decision record: [ADR-0031](../../../docs/adr/0031/0031-structural-knowledge-graph.md).

## Run

Start and load the Phase 6 graph first:

```powershell
docker compose --profile kg up -d neo4j
.\tools\kg\load-graph.ps1
```

Then run the server from the repository root:

```powershell
$env:KG_NEO4J_URL = "bolt://localhost:7687"
dotnet run --project tools/kg/kg-mcp/KgMcp.Server/KgMcp.Server.csproj --no-launch-profile
```

The server is stdio-only. Diagnostics go to stderr so stdout remains a clean MCP JSON-RPC channel. It never writes to Neo4j; regenerate and reload the graph instead.

Startup does not run a database migration or load the graph. The `docker compose` and `load-graph.ps1` commands above are the explicit infrastructure steps; the server only opens a read-only Bolt connection when a tool is called. After the process is ready, stderr prints `[kg-mcp] ready; transport=stdio; ... migrations=none; graph-load=external`. It then waits for MCP JSON-RPC messages on stdin, so an idle process is expected and is not a hung migration.

## Tests

```powershell
dotnet test tools/kg/kg-mcp/KgMcp.sln --nologo
```

Three suites, and **each one catches a class the other two structurally cannot** — so the useful question when adding a traversal is not "is it tested" but "which suite can even observe this being wrong". Six defects have shipped past a green suite here, every one of them because the answer was the wrong one.

| Suite | Graph it runs against | Needs Docker | What only it can catch |
| --- | --- | --- | --- |
| `ContractTests` | none — reads source text | no | Rules about the *shape of the code*: no Cypher outside `Core`, no write clause, no tool named as if it answered an out-of-scope question, no id-taking traversal skipping `RequireLabelAsync`. These outlive the ten tools that exist today. |
| `GraphServiceTests` | `Neo4jFixture` — hand-built, ephemeral | yes | Exact named behaviour: this id resolves to that surface, this orphan grades `ambiguous` and that one `high`. Precise, and blind to any shape nobody thought to add. |
| `RealGraphE2ETests` | this repository's own graph, regenerated every run | yes | Whatever the codebase really contains. Both post-Phase-7 defects were invisible to the fixture and obvious here: one needed a node reachable by two path lengths, the other needed a real id to typo. |

`RealGraphFixture` runs the actual `kg-codegen` executable, loads its output into a throwaway Neo4j container, and takes ~56 s — source → parsers → Cypher → database → traversal with nothing stubbed. The load is itself an assertion: a seed emitting `key: null` again would be rejected and take the class down at startup.

It **reads no seed file from disk** (seeds are gitignored and timestamped, so a suite depending on one would pass or fail by machine) and **hardcodes no expected value**. Per-label counts and the edge total are read back out of what the generator printed on that run; every reported `depth` is cross-checked against Neo4j's `shortestPath`, an oracle sharing no code with the traversal under test. A test that re-issues the implementation's own query proves only that the query is deterministic.

Two rules earned the hard way, both recorded at greater length in [`docs/reference/kg-mcp-tools.md`](../../../docs/reference/kg-mcp-tools.md):

- **A behavioural test is only as good as the topology its fixture contains.** The depth defect survived a green container suite because both depth tests traversed `S1..S8`, the one fixture shape with no branching. Add the shape the code can be wrong about, then break the code and watch the test go red — a test that passes both ways is decoration.
- **Assert the error paths, not only the happy one.** For a query, "unknown id" and "id of the wrong kind" are mandatory assertions: an empty list for either makes a typo indistinguishable from a true negative, which is the root cause of five separate defects in this server.
