# Playbook — Standalone RAG platform for many projects

> Audience: teams that want one reusable RAG stack outside any single app repo,
> then connect multiple projects to it.
>
> Goal: keep retrieval deterministic and predictable, while making onboarding of a
> new project a configuration task, not a reimplementation task.

---

## When to choose standalone

Choose this model when at least one is true:

- You plan to onboard 3+ repositories in the next quarter.
- You need one shared operations surface (monitoring, backups, upgrades).
- You want identical RAG behavior across multiple teams.
- You want app repos to stay focused on product code, not infra runtime code.

If you only have one repo and one team, keep embedded RAG for now.

---

## Topology options

1. Embedded (current ECommerceApp style): RAG files live in the app repo.
2. Sidecar repo: one dedicated infra repo; each app repo is mounted/indexed into it.
3. Central service: one long-running RAG platform with per-project collections.

Recommended path for your June switch:

1. Keep serving production from option 1 (already stable).
2. Build option 2 in parallel as the migration target.
3. Promote to option 3 only after 2-3 projects run cleanly for at least 2 weeks.

---

## Reference architecture (option 2)

Use one repository, for example rag-platform, that contains:

- docker-compose.yaml with qdrant, rag-python-http, rag-dotnet-http
- tools/rag (Python ingest/server/tooling)
- tools/rag-dotnet (optional .NET variant)
- config/projects/<project-id>/metadata-rules.yaml
- config/projects/<project-id>/queries.yaml
- config/projects/<project-id>/multilingual-glossary.yaml
- data/qdrant (persistent volume)
- docs/runbooks for operations, backup, restore

Project content remains in app repositories. The platform accesses docs either via:

- host bind mounts, or
- CI artifact download into a staging folder before ingest.

---

## Stage 1 — Build the platform repo

1. Copy baseline from this repo:

- tools/rag
- tools/rag-dotnet (if needed)
- minimal compose services for qdrant + HTTP servers

2. Keep one global base config template and override per project:

- project id
- collection names
- workspace path
- metadata rules
- named queries

3. Adopt naming convention from day one:

- collection: <project>_docs
- dotnet collection: <project>_docs_dotnet
- optional test collection: <project>_smoke

---

## Stage 2 — Onboard one project

For each project, create:

- config/projects/<project>/metadata-rules.yaml
- config/projects/<project>/queries.yaml
- config/projects/<project>/multilingual-glossary.yaml

Then run ingest with explicit project-scoped settings. Verify:

1. list_adrs returns expected ADR count.
2. query_docs returns expected top hits for 5 known questions.
3. read_docs returns full-file detail for one deep query.
4. get_history returns chunks for at least one ADR id.

Keep this as a mandatory onboarding gate before declaring project ready.

---

## Stage 3 — Multi-project isolation rules

Never share one collection between projects.

Required isolation:

- one collection per project and runtime variant
- project-specific metadata-rules and queries
- explicit project selection in HTTP calls when using shared endpoints

Operational isolation:

- per-project ingest logs
- per-project parity report
- per-project backup/restore procedure

---

## Stage 4 — MCP client routing for many projects

Use explicit server names per project:

- rag-<project>-python
- rag-<project>-dotnet

For shared endpoint deployments, route by project identifier (for example with
project query parameter support) and keep project selection explicit in tests and
automation to avoid cross-project leakage.

---

## Stage 5 — June switch readiness checklist

Minimum bar to call the setup ready:

- real_mcp_check.py passes end-to-end on current project
- 2 consecutive reruns pass (stability, not one-shot success)
- startup warmup behavior documented (first-call transient failures known)
- backup + restore of Qdrant tested once
- fallback endpoint logic verified for .NET remote ingest
- smoke test command published for on-call use

Suggested SLOs for retrieval quality:

- query_docs top-1 relevance >= 80% on named eval queries
- Python/.NET parity >= 60% top-1 on shared eval set
- get_history correctness 100% for tracked ADR ids

---

## Feedback loop design (future Ollama layer)

Your direction is correct: keep retrieval deterministic, add intent interpretation
on top.

Pragmatic sequence:

1. Keep RAG as source-of-truth retriever (stateless and predictable).
2. Add a lightweight intent-router model (for example Ollama) that maps user
   intent to tool strategy: query_docs vs read_docs vs get_history.
3. Keep router output observable (log prompt, chosen tool, and confidence).
4. Keep hard guardrails: if confidence is low, call query_docs first.
5. Evaluate router decisions against named queries weekly.

Do not merge generation confidence with retrieval confidence; track both
separately.

---

## Anti-patterns to avoid

- One shared collection for all projects.
- Glossary shared blindly across unrelated domains.
- Manual ingestion without manifest/checkpoint outputs.
- Treating one successful run as production readiness.
- Hiding project routing inside undocumented defaults.

---

## Suggested rollout plan

1. Week 1: keep current embedded stack as production baseline.
2. Week 1-2: spin up standalone platform repo in parallel.
3. Week 2: onboard ECommerceApp as first tenant and run parity checks.
4. Week 3: onboard second repo and validate isolation.
5. Week 4: cut over MCP clients to standalone endpoints.

---

## References

- [rag-bootstrap.md](rag-bootstrap.md)
- [context-mode-bootstrap.md](context-mode-bootstrap.md)
- [SETUP-GUIDE.md](../rag/SETUP-GUIDE.md)
- [rag-architecture.md](../rag/rag-architecture.md)