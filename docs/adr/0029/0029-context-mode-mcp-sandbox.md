# ADR-0029: context-mode MCP sandbox with DNS-level egress firewall

## Status

**DRAFT — Proposed.** Not yet accepted. Concept agreed in design discussion; full plan captured to avoid loss of context. Acceptance pending implementation kick-off (after RAG MCP stabilisation is done).

## Date

2026-05-26

## Context

GitHub Copilot is moving to request-usage billing. Raw tool output (terminal stdout,
file reads, web fetches) is dumped into the model context window, which directly
increases per-request token cost and shortens productive session length.

Measured baseline raw output for a typical working session: **315 KB of context per session**,
of which only a fraction is actually used by the model for reasoning. Examples:

| Tool call | Raw output | Useful content |
|---|---|---|
| Playwright snapshot | 56 KB | 299 B |
| GitHub issues list (20) | 59 KB | 1.1 KB |
| Test suite run (30 tests) | 6 KB | 337 B |
| Repo research subagent | 986 KB | 62 KB |

An external MCP server, [`context-mode`](https://github.com/mksglu/context-mode) (v1.0.146,
Elastic License 2.0), implements a session-scoped sandbox that executes tools inside its
own process, persists the raw output in a local SQLite/FTS5 store, and returns to the
model only a small summary plus a stable handle. Re-queries hit the local store, not the
context window.

The existing RAG MCP server (`ecommerceapp-rag`, ADR-0028) covers project documentation
search and stays in place. context-mode is complementary: it sandboxes *runtime* tool I/O
that RAG does not cover.

### Forces

- Need to reduce Copilot context usage without changing how the team works
- Must not interfere with existing RAG MCP server, agents, or `.github/copilot-instructions.md`
- Must run on Docker Desktop (Windows), Rancher Desktop, and Podman — no kernel features that
  differ across runtimes
- Cannot trust an arbitrary third-party MCP server with unrestricted network access — it sees
  every tool argument, file path, and code snippet
- Team must adopt with a single `docker compose up -d` — no per-developer install
- Configuration must live in the repo so it is reviewable, versioned, and team-shared

## Decision

We will run `context-mode` as a hardened Docker container with:

1. **Build from source, pinned tag.** The image is built from `git clone --branch v1.0.146`,
   not `npm install -g`. Upgrades happen by changing a single `ARG CONTEXT_MODE_TAG` line
   in the Dockerfile, then a controlled commit.

2. **Defence-in-depth container hardening.** `read_only: true` + `tmpfs: /tmp`,
   `cap_drop: [ALL]`, `security_opt: [no-new-privileges:true]`, `pids_limit: 100`,
   `mem_limit: 1g`, `ipc: none`, non-root user `ctxmode`.

3. **Custom bridge network `ctx-net`** instead of `--network none`. All container DNS
   traffic is forced through the AdGuard container (`dns: [adguard]`). This trades a small
   amount of "absolute isolation" for the ability to use `ctx_fetch_and_index` under
   controlled allowlist conditions, and gives us a query log for every domain lookup.

4. **AdGuard Home as DNS-level egress firewall.** Community blocklists auto-update every
   168 hours (7 days). A team-shared allowlist and blacklist (file-based, committed to
   the repo, PR-reviewed) override community decisions when needed. Individual developers
   can keep `personal-overrides.local.txt` (gitignored) for experiments. Per-client policies
   apply strict rules to `context-mode` and permissive rules to existing project
   containers (RAG, Qdrant).

5. **Node.js network monitor hook.** A small `--require`'d module patches
   `net.Socket.prototype.connect` before the MCP server starts and logs every connection
   attempt to stderr plus a workspace-mounted alert file. This is a secondary signal —
   AdGuard provides the primary block; the hook proves what the application *tried* to do.

6. **Append-only integration with existing Copilot config.** New section 13 added to
   `.github/copilot-instructions.md`. Sections 1–12 (project agents, ADR routing, BC rules)
   stay untouched. RAG MCP and all current agents continue to work unchanged.

7. **Hooks via `docker exec`.** `.github/hooks/context-mode.json` runs PreToolUse,
   PostToolUse, and SessionStart hooks via `docker exec -i ecommerceapp-context-mode ...`
   so no developer needs the `context-mode` CLI installed on the host.

8. **Container logs via VS Code Docker extension / `docker logs`.** No dedicated log
   viewer container (Dozzle, Portainer) is added. VS Code's built-in Docker extension
   plus `docker logs <container>` covers the team's debugging needs without adding a
   third long-running service. If a richer UI becomes necessary later, Dozzle can be
   added behind `--profile monitoring` without changing this ADR.

## Consequences

### Positive

- Measured target: ~98% reduction in context window consumption per session (315 KB → 5.4 KB).
- Session continuity preserved across Copilot's automatic context compaction events.
- Full audit trail of every DNS lookup originating from project containers, file-based and
  team-reviewable.
- Hardening flags reduce blast radius of a hypothetical context-mode compromise to "container
  with no capabilities, no fs writes, restricted PIDs, no IPC, no DNS resolution to
  non-allowed domains".
- Existing RAG MCP and agent configuration unaffected.
- Multi-runtime: `--network none` removed in favour of bridge + AdGuard, but everything still
  works identically on Docker Desktop / Rancher Desktop / Podman (only the CLI keyword
  changes — local swap, not a repo change).
- Team-shared blacklist/allowlist via PR flow means a security finding by one developer
  propagates to everyone with `git pull` + AdGuard restart.

### Negative

- Two new containers to maintain (context-mode always-on, AdGuard behind `--profile
  monitoring`) on top of existing Qdrant and RAG containers. Kept deliberately small
  to avoid "another stack the team has to learn".
- AdGuard YAML and 4 text files (community-blocklists.yaml, team-blacklist.txt,
  team-whitelist.txt, personal-overrides.local.example.txt) become new repo artefacts.
- DNS-level filtering does not block plain-IP connections (no DNS lookup → no filter
  decision). For our threat model (third-party Node MCP server), this is acceptable —
  the hardening flags cover the rest.
- ELv2 (Elastic License 2.0) on context-mode forbids offering it as a hosted SaaS.
  Internal team use is permitted, which is our only use case.
- **Docker Desktop licensing caveat for large companies.** Docker Desktop is paid for
  organisations with >250 employees OR >$10M annual revenue. This setup does not depend
  on Docker Desktop features — Rancher Desktop (Apache 2.0) and Podman (Apache 2.0)
  work identically and are free at any company size. Linux dev hosts can use Docker
  Engine directly (also free). All commands in this repo use the `docker` CLI verb;
  swap to `podman` / `nerdctl` is a per-developer local change, not a repo change.
  Self-hosted Qdrant (Apache 2.0) has no equivalent restriction at any scale.

### Risks & mitigations

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| context-mode breaking change after upgrade | Low (pinned tag) | Medium | Single-commit upgrade with team review; `ctx doctor` verification step |
| AdGuard blocks a domain we actually need | Medium | Low | UI quick-fix + PR to `team-whitelist.txt` for permanent team-wide override |
| Hooks interfere with RAG MCP calls | Low | Low | `CONTEXT_MODE_EXTERNAL_MCP_NUDGE_EVERY=50` reduces hook-injected guidance frequency |
| Append to `copilot-instructions.md` conflicts with existing rules | Very Low | High | Section 13 has lowest priority; project rules (BC, ADR) take precedence; reviewed in PR |
| Multi-runtime quirks (Podman/nerdctl) | Medium | Low | Local CLI swap documented in roadmap details file; not committed |
| AdGuard misconfiguration locks out maintenance | Low | Medium | AdGuard UI accessible even when filtering active; emergency switch is `docker compose stop adguard` |
| Community blocklist auto-update introduces false positives | Medium | Low | Update interval 7 days (not 24h) gives team time to react; `team-whitelist.txt` overrides any community decision |

## Alternatives considered

- **Do nothing, accept Copilot billing as-is.** Rejected — operational cost grows with team
  productivity, and the team's session lengths are already constrained by context window
  limits regardless of billing.
- **`--network none` only, no AdGuard.** Rejected — gives absolute isolation but disables
  `ctx_fetch_and_index` permanently and provides no visibility into what context-mode would
  have tried to connect to.
- **mitmproxy egress proxy with HTTP allowlist.** Rejected for now — works at HTTP layer
  (handles HTTPS via cert injection, more invasive), heavier maintenance than AdGuard,
  no built-in community lists, no first-class UI for non-tech reviewers. Reconsider if
  we ever need per-URL (not per-domain) filtering.
- **Dozzle as a dedicated log viewer container.** Rejected for the main stack — VS
  Code's built-in Docker extension plus `docker logs` already covers the team's
  debugging needs without adding a third long-running service. Dozzle stays as a
  documented "if you want it" option behind `--profile monitoring`, but is not part
  of the default plan and is not in the conformance checklist. Re-evaluate if the
  team grows beyond easy CLI-based log inspection.
- **Portainer for unified container management.** Rejected — Portainer manages containers
  and Docker networks but does *not* filter URLs or domains. Its features overlap with
  existing `docker compose` tasks in VS Code and the Docker extension. Adds ~80 MB idle
  RAM and a new admin password to maintain for marginal gain in a single-dev workflow.
  Re-evaluate if team grows or multi-environment management becomes a need.
- **Automated threat-feed import to `team-blacklist.txt` ("Phase 6" / no-review
  pipeline).** Rejected for the initial cut — AdGuard's 3 built-in community lists
  already cover ~95% of relevant bad domains via maintainers who curate full-time.
  Adding GitHub Action + bot commits + suggestions buffer increases moving parts for
  marginal coverage. May be reconsidered as a future amendment if the team observes
  a recurring class of bad domains slipping past the community lists; design sketch
  kept in `docs/roadmap/context-mode-integration.md` (Phase 6 — future).
- **Falco / Tracee for syscall-level monitoring.** Rejected — requires privileged container,
  generates high-volume logs that need tuning, overlaps with what `--cap-drop ALL` +
  AdGuard already prevent. Reconsider if a real incident shows a syscall-level gap.
- **`npm install -g context-mode` per developer.** Rejected — no audit, every developer on
  whatever version `npm` happened to resolve that day, no enforced sandboxing.
- **In-house build of an MCP context-reduction layer.** Rejected — context-mode already
  solves the problem, has 172 releases of iteration, and ELv2 permits internal use.

## Migration plan

Implementation is captured in [`docs/roadmap/context-mode-integration.md`](../../roadmap/context-mode-integration.md)
and [`docs/roadmap/context-mode-details.md`](../../roadmap/context-mode-details.md). Summary:

1. **Phase 1** — Dockerfile, network monitor, hardening flags, `ctx-net` bridge, compose entry
2. **Phase 2** — `.vscode/mcp.json`, VS Code tasks, `.gitignore`
3. **Phase 3** — `.github/hooks/context-mode.json`
4. **Phase 4** — Append section 13 to `.github/copilot-instructions.md`; invoke `@copilot-setup-maintainer`
5. **Phase 5** — AdGuard service (`--profile monitoring`) + 4 config files in `docker/adguard/` + per-client policies

Each phase has its own acceptance criteria in the roadmap files. Phases 3+ can be skipped
or deferred without breaking earlier phases.

**Future amendment (not in this ADR cut):** A possible Phase 6 — automated triage of
AdGuard query log into a reviewable `docker/adguard/suggestions.json` buffer plus a
VS Code Problem Matcher that surfaces "new suggestions arrived" as yellow warnings in
the Problems panel. Not committed because community-list auto-update already covers
the common case; revisit if real usage shows the gap.

## Conformance checklist

- [ ] `Dockerfile-context-mode` builds from `git clone --branch <pinned-tag>`, not from npm
- [ ] context-mode service in `docker-compose.yaml` has all 6 hardening flags
  (`read_only`, `cap_drop: [ALL]`, `security_opt: [no-new-privileges:true]`, `pids_limit`,
  `mem_limit`, `ipc: none`)
- [ ] context-mode service uses `ctx-net` bridge and `dns: [adguard]`, never `network_mode: host`
- [ ] AdGuard config files (`AdGuardHome.yaml`, `community-blocklists.yaml`,
  `team-blacklist.txt`, `team-whitelist.txt`, `personal-overrides.local.example.txt`)
  exist in `docker/adguard/` and are referenced from compose
- [ ] `personal-overrides.local.txt` is in `.gitignore`
- [ ] `network-monitor.js` is loaded via `node --require` before context-mode's entrypoint
- [ ] `.github/copilot-instructions.md` section 13 is append-only; sections 1–12 unchanged
- [ ] `.github/hooks/context-mode.json` uses `docker exec`, not a host-installed CLI
- [ ] AdGuard is gated by `profiles: [monitoring]` — never starts by default
- [ ] `ctx_fetch_and_index` is documented as gated by AdGuard allowlist
- [ ] Upgrade procedure is a single ARG change in the Dockerfile
- [ ] Docker Desktop licensing caveat documented; Rancher/Podman swap is a local-only
      change (not committed)

## References

- [context-mode (mksglu/context-mode)](https://github.com/mksglu/context-mode) — Elastic License 2.0
- [AdGuard Home](https://github.com/AdguardTeam/AdGuardHome) — GPL-3.0
- [Dozzle](https://github.com/amir20/dozzle) — MIT
- [ADR-0028](../0028/0028-remote-multitenant-rag-ingest.md) — RAG MCP architecture (complementary)
- Roadmap: [`docs/roadmap/context-mode-integration.md`](../../roadmap/context-mode-integration.md)
- Details: [`docs/roadmap/context-mode-details.md`](../../roadmap/context-mode-details.md)
- StevenBlack/hosts — MIT
- AdGuard SDN Filter — CC BY-SA 4.0
- EasyList / EasyPrivacy — CC BY-SA 3.0
