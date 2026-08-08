# `kg-mcp`

`kg-mcp` is the read-only, stdio MCP facade over the Neo4j graph produced by `kg-codegen`. It exposes exactly the ten Tier-1 structural queries defined in [the ontology design](../../../docs/architecture/knowledge-graph-ontology-design.md). The model itself is not documented here; the design document is the source of truth for the ontology and tool semantics.

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
