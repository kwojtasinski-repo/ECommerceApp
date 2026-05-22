# ADR-0028 Router

Main decision: [0028-remote-multitenant-rag-ingest.md](0028-remote-multitenant-rag-ingest.md)

Technical implementation details:
- [.NET server — data model, API contract, caching, DI wiring](tech-details-dotnet.md)
- Python server — *(planned — Phase 2)*
- [Amendment 001: implementation deviations](amendments/0028-001-implementation-deviations.md)

Topics: RAG, MCP, multi-tenant, remote deployment, async ingest, Qdrant, API key,
SSE, collection identity, full content storage, operation status, ingest CLI, .NET, Python.
