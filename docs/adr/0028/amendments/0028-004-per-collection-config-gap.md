# ADR-0028 Amendment 4: Per-Collection Config Persistence Gap

## Date
2026-05-28

## Author
GitHub Copilot (parity audit diagnosis session)

## Summary

The "config travels with the docs" model from the main ADR-0028 text and
[`tech-details-dotnet.md`](../tech-details-dotnet.md) is **not implemented** in production
code. `QdrantDocumentStore.StoreConfigAsync()` and `FetchConfigAsync()` exist and are
correct, but they are never called outside unit tests.

[Amendment 002](./0028-002-batch-manifest-pipeline.md) explicitly deferred the design with
"The config files are NOT indexed — they are extracted for validation/configuration only",
but neither the main ADR text nor the tech-details files were updated to reflect that
deferral. This amendment closes that documentation gap and points to the fix plan in the
roadmap.

This amendment is **documentation-only**. The actual code fix lives in
[`docs/roadmap/rag-remote-multitenant.md` § Phase 3](../../../roadmap/rag-remote-multitenant.md#phase-3--per-collection-config-persistence-gap-fix).

---

## Discovery

Found during the RAG parity audit on 2026-05-28 — see
[`docs/reports/rag-parity-fix-diagnosis-2026-05-28.md`](../../../reports/rag-parity-fix-diagnosis-2026-05-28.md).

While verifying whether weight and glossary changes applied earlier the same day actually
took effect on the production HTTP code path, the diagnosis found:

1. `BatchIngestService` parses the ZIP's `rag-config.yaml`, `metadata-rules.yaml`,
   `queries.yaml`, and optional `multilingual-glossary.yaml` purely for validation and to
   extract `doc_kind` / `adr_id` rules. Everything else is discarded.
2. `RagQueryService.QueryAsync()` and `RagReadDocsService.ReadAsync()` always read
   `cfg.Query.FetchK`, `cfg.Query.ScoreThreshold`, and the weight table from the singleton
   `RagConfig` loaded once at startup from the mounted `rag-config.yaml`.
3. `GlossaryExpansionPreprocessor` is constructed at startup from
   `MultilingualGlossary.Load(cfg.GlossaryPath)`. It is collection-agnostic.
4. `grep StoreConfigAsync tools/rag-dotnet/src/` returns zero production call sites.

---

## Impact

Without per-collection config persistence:

- **Multi-tenancy is broken in production.** Two collections on the same server share one
  set of weights, one score threshold, one fetchK, and one glossary. The design intent in
  ADR-0028 promised per-collection tuning; the reality is single-tenant tuning that happens
  to work because (a) only one large project is ingested today, and (b) the mounted config
  is curated to match it.
- **The R3 glossary fix from 2026-05-28** (canonical glossary mounted into the
  `rag-dotnet-http` container) works, but only because it ends up acting as a server-wide
  setting. The same fix would not survive the introduction of a second project with
  different multilingual needs.
- **Acceptance criteria in the roadmap** that read *"Config, glossary, rules, queries
  stored as structured JSON (not raw YAML text)"* are not met. They were marked as
  Phase-2-complete in error.
- **Edge case "Upload with no config (subsequent re-upload) — Use last stored config from
  Qdrant"** in the roadmap is unreachable because no collection has a stored config.

---

## Why this was not caught earlier

- Amendment 002 introduced the batch-manifest pipeline and stated config files are not
  indexed, but the main ADR text was not updated. A reader of the main ADR would still
  expect per-collection storage.
- The infrastructure (`StoreConfigAsync`, `FetchConfigAsync`, `RagConfigPayload`) was
  shipped and unit-tested, so code inspection alone suggests the feature works.
- The single-project deployment masked the missing behavior: there was no second collection
  whose different settings would have failed.
- The 2026-05-28 parity audit was the first time someone asked specifically "do the
  changes I'm making take effect on the production code path?" rather than treating the
  mounted config as a foregone source of truth.
- **One hypothesis worth recording as falsified**: the `MCP_TRANSPORT` env var (toggles
  stdio vs HTTP transport) does NOT switch to a per-collection persistence flow in HTTP
  mode. Both transports register identical `IDocumentStore`, `BatchIngestService`,
  `IngestWorker`, `RagQueryService`, `GlossaryExpansionPreprocessor` services in
  `Program.cs`. The HTTP branch only adds controllers, the API key middleware, and Kestrel
  binding — it does not change which config the query layer reads or whether batch ingest
  persists config. Verified by grep across `tools/rag-dotnet/src/` (zero call sites for
  `StoreConfigAsync` outside mocks and the `CachedDocumentStore` pass-through).


---

## Decision

1. **Acknowledge the gap.** The promise in the main ADR-0028 text and `tech-details-dotnet.md`
   is not currently honored. Treat it as deferred work, not a design change.
2. **Fix in Phase 3 of the roadmap.** Concrete steps P3-1 through P3-8 in
   [`docs/roadmap/rag-remote-multitenant.md`](../../../roadmap/rag-remote-multitenant.md#phase-3--per-collection-config-persistence-gap-fix).
3. **Keep R1, R2, R3 (parity tuning shipped 2026-05-28) as the single-tenant stopgap.** They
   are safe and effective until a second project is added.
4. **Do NOT amend the main ADR text yet.** Wait until Phase 3 lands; then either align the
   main ADR with the implementation (preferred) or, if the design is intentionally simplified
   to "server-side configs only", document that as a deliberate scope reduction.

---

## Updated guidance for readers of ADR-0028

Until Phase 3 lands, read the main ADR and `tech-details-dotnet.md` with this caveat:

> The `RagConfigPayload`, `StoreConfigAsync`, and `FetchConfigAsync` machinery is present
> in code but currently dead. The .NET HTTP server uses its mounted `rag-config.yaml`,
> `multilingual-glossary.yaml`, `metadata-rules.yaml`, and `queries.yaml` for every
> request, regardless of which collection the request targets. Per-collection persistence
> is roadmap Phase 3 (see `docs/roadmap/rag-remote-multitenant.md`).

This caveat will be removed once Phase 3 P3-6 (ADR update) is complete.

---

## References

- Main ADR: [`docs/adr/0028/0028-remote-multitenant-rag-ingest.md`](../0028-remote-multitenant-rag-ingest.md)
- Conflicting amendment: [`0028-002-batch-manifest-pipeline.md`](./0028-002-batch-manifest-pipeline.md) (deferred config indexing without updating main text)
- Tech details still showing original intent: [`tech-details-dotnet.md`](../tech-details-dotnet.md)
- Diagnosis report: [`docs/reports/rag-parity-fix-diagnosis-2026-05-28.md`](../../../reports/rag-parity-fix-diagnosis-2026-05-28.md)
- Fix plan: [`docs/roadmap/rag-remote-multitenant.md` § Phase 3](../../../roadmap/rag-remote-multitenant.md#phase-3--per-collection-config-persistence-gap-fix)
- Anomalies log: `/memories/repo/rag-mcp-anomalies.md`
