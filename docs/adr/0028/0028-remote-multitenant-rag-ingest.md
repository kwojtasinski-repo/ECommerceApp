# ADR-0028: Remote-Capable Multi-Tenant RAG Server with Async Ingest API

## Status
Accepted � Phase 1 (.NET) Implemented

## Date
2026-05-21

## Context

ADR-0027 established a local RAG pipeline where the server reads documentation directly from
a mounted filesystem volume and serves a single fixed project per server instance.

This created two blockers for shared team deployment:

1. **Filesystem coupling**: the server must have direct access to the developer's local workspace.
   A remotely hosted server cannot read files from a developer's machine.

2. **One project per server**: serving multiple projects requires multiple server instances,
   each loading the full 470 MB AI model into memory -- wasteful for a small team.

Additional requirements surfaced during design:
- Developers push docs to the server; the server does not pull from their machines.
- Ingesting hundreds of files can take 10-30 seconds; uploads must return immediately.
- Configuration (chunking settings, glossary, weights) varies per project and must travel with the docs.
- A small team on a trusted network needs simple, low-friction authentication.

---

## Decision

**Run one shared server that hosts any number of projects.**
Each project pushes its documentation to the server via an HTTP API.
The server indexes the docs and answers questions for that project via a session-bound MCP connection.

### Three-part architecture

#### 1. Project identity from configuration

Each project is identified by a `collection` name in its `rag-config.yaml`.
This name is used everywhere: as the index name in the vector database, as the URL segment
in the ingest API, and as the session selector in MCP connections.

No registration step is needed. Pushing docs to a collection creates it automatically.

#### 2. Session-bound project selection

When a developer connects their AI assistant to the shared server, they declare which project
they belong to in their connection URL:

`http://rag.internal:3001/?project=myproject`

All questions asked in that session are answered using that project's indexed documentation.
The developer never needs to specify the project again -- not per-query, not per-tool-call.

#### 3. Async document upload

Developers upload documentation from their local machine using the ingest CLI:

`dotnet ingest --remote http://rag.internal:3001`

The server receives the files, returns a job ID immediately, and indexes the docs in the
background. If the same file is pushed again, the server safely replaces the old version.

A shared API key (`RAG_API_KEY`) protects all upload endpoints. Querying is not protected --
it is assumed to happen within a trusted team network.

---

## Consequences

### Benefits

- **One model, many projects**: the AI model is loaded once and shared across all projects on
  the server -- significant memory saving vs. one server per project.
- **No volume mounts**: documentation is stored in the vector database. Server restarts or
  migrations do not lose indexed content.
- **Idempotent uploads**: pushing docs again is always safe. Changed files are replaced; unchanged
  files are skipped via manifest tracking.
- **Backward compatible**: local Docker and stdio mode from ADR-0027 continue to work unchanged.

### Trade-offs

- **Ingest step required**: a fresh server answers no questions until docs are uploaded at least once.
- **Single API key**: rotating the key requires a server restart. Acceptable for a small team.
- **Server crash mid-ingest**: an in-flight job shows as `processing` indefinitely.
  Callers detect stale jobs by checking job age (> 1 hour in processing = stale).

### Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Large upload causes memory spike | Low-Medium | Per-file size limit enforced at upload |
| API key leaked | Low | Treat as internal secret; rotate via env var restart |
| Partial upload leaves inconsistent index | Low | File-level upserts, not collection wipes |

---

## Alternatives Considered

**A - Keep filesystem mounts (ADR-0027 design)**
Works for local Docker. Cannot work for a remotely hosted server. Kept for local dev.

**B - Server pulls docs from a shared git repo**
Cleaner for CI. Rejected: requires the server to have git access and know when to pull.
Push-based upload keeps the developer in control.

**C - One server per project**
No multi-tenancy complexity. Rejected: doubles memory cost (full AI model load) per added project.

**D - Pass project on every tool call**
Avoids session binding. Rejected: forces the AI assistant to always include the parameter,
which is verbose and error-prone.

**E - JWT or OAuth authentication**
Per-user/project tokens. Rejected at this scale (2-5 people, trusted network).
A shared API key is sufficient and far simpler to operate.

**F - Separate database for operation status**
More durable. Rejected: adds a runtime dependency. In-memory tracking with 1-hour retention
is sufficient for the polling use case.

---

## Migration from ADR-0027 (local setup)

Existing local setups require **no changes**. `RAG_COLLECTION`, volume mounts, and stdio
transport from ADR-0027 continue to work exactly as before.

To switch to the shared team server:

1. Host the server with `RAG_API_KEY` set.
2. Run the ingest CLI once per project: `--remote http://rag.internal:3001`
3. Update `.vscode/mcp.json` to use the remote SSE URL with `?project=<name>`.
4. Keep local Docker entries in mcp.json as a fallback.

---

## Technical Reference

Implementation details (data formats, API contracts, caching strategy, component design,
and deviations from this ADR) are in the technology-specific companion files:

- [.NET implementation details](tech-details-dotnet.md)
- Python implementation details *(planned -- Phase 2)*
- [Amendment 001: implementation deviations](amendments/0028-001-implementation-deviations.md)
- [Amendment 002: batch manifest design, per-op manifest, ZIP validation, pipeline detail](amendments/0028-002-batch-manifest-pipeline.md)
- [Amendment 003: transport-aware tools — `ListAdrs` Qdrant source, `RagSession` scope fix, `IContentSource`](amendments/0028-003-transport-aware-tools.md)
