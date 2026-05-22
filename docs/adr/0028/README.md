# ADR-0028 Router

Main decision: [0028-remote-multitenant-rag-ingest.md](0028-remote-multitenant-rag-ingest.md)

Technical implementation details:
- [.NET server — data model, API contract, caching, DI wiring](tech-details-dotnet.md)
- [Python server — data model, async queue, middleware, wiring](tech-details-python.md)
- [Amendment 001: implementation deviations](amendments/0028-001-implementation-deviations.md)

**Retention note (both implementations):** In-memory `OperationStore` retains operations
for **1 hour** after `enqueued_at`. Operations are lost on server restart. Both the .NET
(`RetentionPeriod = TimeSpan.FromHours(1)`) and Python (`RETENTION_HOURS = 1`) constants
are identical by design. Persistent storage via Qdrant is deferred to a future phase.

Topics: RAG, MCP, multi-tenant, remote deployment, async ingest, Qdrant, API key,
SSE, collection identity, full content storage, operation status, ingest CLI, .NET, Python.
