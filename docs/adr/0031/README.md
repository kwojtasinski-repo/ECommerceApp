# ADR-0031 — code-derived structural knowledge graph

> **Status: Accepted — all seven build phases implemented and validated 2026-08-08.**

## Files in this folder

| File | Purpose |
|---|---|
| [`0031-structural-knowledge-graph.md`](./0031-structural-knowledge-graph.md) | Main ADR — decision, extraction rules, build status, alternatives |

## Related

- Design + phase ledger: [`docs/architecture/knowledge-graph-ontology-design.md`](../../architecture/knowledge-graph-ontology-design.md)
- Tool: [`tools/kg/kg-codegen/README.md`](../../../tools/kg/kg-codegen/README.md)
- MCP tool contracts: [`docs/reference/kg-mcp-tools.md`](../../reference/kg-mcp-tools.md)
- GitHub Copilot query skill: [`.github/skills/ecommerceapp-kg-query/SKILL.md`](../../../.github/skills/ecommerceapp-kg-query/SKILL.md)
- Claude Code query skill: [`.claude/skills/ecommerceapp-kg-query/SKILL.md`](../../../.claude/skills/ecommerceapp-kg-query/SKILL.md)
- Ontology seed: `tools/kg/seed/ontology.json`, `tools/kg/seed/ontology.cypher`
- Complementary retrieval ADR: [`0028`](../0028) (RAG over docs)
