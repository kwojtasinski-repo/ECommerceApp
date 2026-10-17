# Supplementary Independent Implementations

Date: 2026-10-17. Status: PARTIAL / PARENT RECOVERED EXTERNAL ACCESS; see recovery evidence below.

Evidence gathering only. No final synthesis, recommendation, downloaded-code execution, or runtime testing. This file does not upgrade baseline README claims into implementation evidence.

## Access Limitation

The current session lists integrated browser and GitHub tools as deferred and explicitly requires loading them through `tool_search` before use. No `tool_search` callable is exposed in this session. Consequently no external retrieval was attempted through those deferred tools. This is a session capability limitation, not evidence that any repository or website is unavailable. Raw `fetch_webpage`, shell HTTP retrieval, and retired context-mode were not used as substitutes.

External pages opened in this pass: **0**. There are no newly accessed external URLs, external quotes, inspected external code lines, revision pins, or test results to report. The access date below applies to local source inspection only.

## Sources Inspected

All local sources below were read on 2026-10-17. They are working-tree documents, not commit-pinned external sources. Reliability is high for what these local documents state, not for independently establishing the external behavior they describe.

| Source | Type | Locator | Revision / Limits |
| --- | --- | --- | --- |
| Baseline external findings | Local prior research | [external-repos-blogs-issues.md](external-repos-blogs-issues.md#L3) | Working tree; reports earlier retrieval, not external retrieval in this pass. |
| Approved supplementary plan | Local approved research scope | [supplementary-research-plan.md](../../planning/supplementary-research-plan.md#L47) | Working tree; defines targets, not implementation facts. |
| Original research plan | Local workflow | [research-plan.md](../../planning/research-plan.md) | Read for scope and evidence requirements; superseded by the current request where external retrieval rules differ. |
| Original source manifest | Local source inventory | [sources.md](../../planning/sources.md) | Read for provenance; its historical "read" statuses are not this pass's access record. |
| Research-gatherer skill | Local workflow | [SKILL.md](../../../../../.github/skills/research-gatherer/SKILL.md) | Read for gathering and approval gates; no synthesis approval inferred. |

## Evidence Register

These three IDs identify **local baseline/scope observations**, not new external implementation evidence.

| ID | Observed Evidence and Exact Quote | Source / Locator | Confidence and Limits |
| --- | --- | --- | --- |
| SI01 | The baseline explicitly excludes source-code inspection: "Only README / issue pages were opened; source code of external repos was **not** read." | [Baseline, line 3](external-repos-blogs-issues.md#L3), accessed 2026-10-17. | High for the baseline's stated scope. Does not establish actual enforcement, parser behavior, test coverage, or runtime reliability of R1-R3. |
| SI02 | The baseline records discovery leads rather than inspected implementations: "Used only as a pointer list. Lists further hook projects (not opened)". | [Baseline R4, line 12](external-repos-blogs-issues.md#L12), accessed 2026-10-17. | High for the recorded discovery status. Dippy, Plannotator, and agnix remain unverified leads here; their listed features are not independently confirmed. |
| SI03 | The approved evidence target is: "Target two independent maintainers beyond baseline R1-R3 and three implementation/test traces; report shortfalls rather than inventing coverage." | [Supplementary plan, line 47](../../planning/supplementary-research-plan.md#L47), accessed 2026-10-17. | High for the acceptance target only. No maintainer identity or implementation trace is established by a target. |

## Rejected or Deferred Leads

The URLs below are copied from the approved plan or baseline as discovery pointers only. **None was opened externally in this pass.** Unknown repository identities remain unknown rather than being guessed. No maintainer, deployment, test, or adoption count is inferred from this list.

| Lead | Provenance | Opened URL / Revision | Disposition |
| --- | --- | --- | --- |
| diet103/claude-code-infrastructure-showcase | [Plan source manifest](../../planning/supplementary-research-plan.md#L23); baseline R1 | None. Candidate: https://github.com/diet103/claude-code-infrastructure-showcase ; unpinned. | Existing maintainer, not a new independent author. Implementation and tests not inspected. |
| nizos/tdd-guard | [Plan source manifest](../../planning/supplementary-research-plan.md#L24); baseline R2 | None. Candidate: https://github.com/nizos/tdd-guard ; unpinned. | Existing maintainer; implementation/test inspection remains outstanding. |
| kenryu42/claude-code-safety-net / cc-safety-net | [Plan source manifest](../../planning/supplementary-research-plan.md#L25); baseline R3 | None. Candidate: https://github.com/kenryu42/claude-code-safety-net ; unpinned. | Existing maintainer; current repository name, redirects, implementation, and tests not independently checked. |
| hesreallyhim/awesome-claude-code | [Baseline R4](external-repos-blogs-issues.md#L12) | None. Candidate: https://github.com/hesreallyhim/awesome-claude-code ; unpinned. | Discovery index only; not a substitute for executable implementation evidence or another deployment. |
| Plannotator | [Baseline R4](external-repos-blogs-issues.md#L12); current user-supplied lead | None; exact repository and revision not discovered. | Plan-review interception is a search hypothesis only. No interception or decision-response code inspected. |
| Dippy | [Baseline R4](external-repos-blogs-issues.md#L12); current user-supplied lead | None; exact repository and revision not discovered. | AST command analysis is a search hypothesis only. No parser, hook adapter, decision policy, or tests inspected. |
| disler/claude-code-hooks-mastery | Current user-supplied lead | None; repository existence and revision not checked. | Observability is a search hypothesis only. No event serialization, persistence, redaction, or tests inspected. |
| agnix | [Baseline R4](external-repos-blogs-issues.md#L12); current user-supplied lead | None; exact repository and revision not discovered. | Configuration validation is a search hypothesis only. No validation rules, diagnostics, or tests inspected. |

Official host semantics and alternatives are assigned to the separate supplementary-hosts-and-alternatives deliverable in the [approved plan](../../planning/supplementary-research-plan.md#L41). They were not silently substituted for independent implementation evidence in this file.

## Preliminary Relevance, Kept Separate

No observed external implementation supports a portability assessment in this pass. Claude Code and Copilot event availability, input/output mapping, blocking behavior, failure mode, and bypass resistance are **unverified**, not equivalent by assumption. The candidate use cases above remain discovery topics, not confirmed transferable patterns. No final recommendation is made.

## Coverage and Verification Limits

| Requirement | Observed Result | Shortfall |
| --- | --- | --- |
| Two new independent maintainers beyond R1-R3 | 0 verified | 2 missing. Repository labels and a curated-list author do not establish independent implementation maintainers. |
| Three implementation/test traces | 0 inspected | 3 missing. No configuration-to-handler-to-test trace was collected. |
| Newly opened external source pages | 0 | The approximately 10-15-page budget was not consumed because the required loading capability is absent. |
| External revision/permalink provenance | None | No external source can be represented as commit-pinned or freshly checked. |
| Runtime reproduction | Not performed | Explicit scope exclusion; static inspection would not itself constitute runtime verification. |
| Deployment count | Unknown | Author count is not deployment count; neither was measured. |
| Baseline preservation | Twelve baseline files hashed before this file was created | Immediate post-write validation must compare all twelve hashes and validate this file's local links and evidence IDs. |

The only added knowledge is an explicit record of the current access blocker and unmet gathering criteria; SI01-SI03 merely preserve the local evidence boundary. No prior conclusion is revised. Artifact checks cannot validate uncollected external behavior.

Copilot setup close-out: audit-only scope; this addition changes no hooks, configuration, architecture decision, router, or Copilot inventory. No cascading edits are authorized. A separate maintainer-agent invocation is not available in this session; no completed maintainer audit is claimed.

INFORMATION GATHERER: evidence captured
Awaiting next instruction or synthesis approval.

## Parent Recovery: Newly Opened Implementation Evidence

The access limitation above applied to the delegated session only. The parent loaded browser and GitHub tools and inspected the following sources on 2026-10-17. Earlier zero-count statements are retained as the original delegate handoff, not the final coverage of this file. SI04-SI08 are newly opened external sources, unlike SI01-SI03. All source paths below are external repository paths, not local workspace files. Moving `main` branches were inspected; no revision pin, installation, runtime execution, production adoption, or test result is claimed.

### SI04 - Approval Bound to Reviewed Content, Not Just a GO Word

- Source: https://raw.githubusercontent.com/backnotprop/plannotator/main/apps/hook/hooks/mod/plan.ts
- Type: executable TypeScript implementation, `planCallAction` and `approvedPermissionDecision`; reliability High for inspected code, Medium for intended integration behavior.
- Exact excerpts: `state.approval.hash === planHash`; `updatedInput: { ...input, plan: approval.plan }`; `return { kind: 'revise', review: state.open }`.
- Observed: approval is matched to the plan hash; the permission response carries the approved plan text. An open review and a matching approval lead to different actions. `normalizePlanForHash` removes trailing whitespace; no caller or cryptographic hash implementation was inspected.
- Exact feedback excerpt: `It is NOT approved. Stay in plan mode and do not implement. End your turn.`
- Additive pattern: content/version-bound human review and actionable waiting feedback, rather than mere session-wide authorization. Binding every later filesystem edit to that approved plan is NOT established by this pure function.
- Code comment attributes `updatedInput` necessity to Claude Code >=2.1.199. This is a maintainer compatibility claim, not independently reproduced vendor behavior.
- Tests for this exact module were not located: the guessed `apps/hook/hooks/mod/plan.test.ts` URL returned 404 (SI08). Do not infer test absence repository-wide.

### SI05 - Stale Payload Recovery and Safe File Reads

- Source: https://raw.githubusercontent.com/backnotprop/plannotator/main/apps/hook/server/claude-plan.ts
- Type: executable TypeScript implementation, `resolveClaudePlan`; reliability High for inspected branches, Medium for the maintainer's timing/version explanation.
- Exact excerpts: `if (!stat.isFile() || stat.size > MAX_ANNOTATABLE_FILE_BYTES) return inlinePlan;`; `return readFileSync(path, "utf8") || inlinePlan;`.
- Observed: a supplied absolute Markdown path is preferred over inline content only after a regular-file and size check; unreadable, empty, invalid-path, or oversized files fall back to inline text.
- Maintainer explanation, exact excerpt: `the inline \`plan\` is the version from before the edit`; comment says Claude Code 2.1.285 fixed this upstream. This historical version claim was not independently verified.
- Additive pattern: treat hook payloads as potentially stale snapshots; constrain file reads to avoid FIFO hangs and unbounded memory. Falling back to inline text trades freshness for availability; it is not proof that a stale plan cannot be approved.

### SI06 - Concrete Regression Tests for the Payload Boundary

- Source: https://raw.githubusercontent.com/backnotprop/plannotator/main/apps/hook/server/claude-plan.test.ts
- Type: Bun test implementation; reliability High for test definitions, no execution result.
- Exact test names: `prefers the plan file over a stale inline snapshot`; `ignores a plan file over the annotate size cap`; `does not block on a FIFO named like a plan file`.
- Observed assertions: revised file content supersedes the first draft; invalid or oversized inputs fall back; the FIFO test uses `test.skipIf(process.platform === "win32")`. Tests create temporary fixtures and clean them after each test.
- Trace: payload-resolution implementation SI05 -> the directly imported `resolveClaudePlan` -> these fixture assertions. This is one implementation/test trace, not three independent projects or a passing test run.
- Relevance: stronger maintenance evidence than a README saying tests exist. Windows FIFO coverage is explicitly absent from that test, not evidence that all Windows path behavior is covered.

### SI07 - Configured Telemetry Versus Optional Network Notifications

- Sources opened:
	- https://raw.githubusercontent.com/disler/claude-code-hooks-mastery/main/.claude/settings.json
	- https://raw.githubusercontent.com/disler/claude-code-hooks-mastery/main/.claude/hooks/stop.py
- Type: executable configuration plus Python implementation; reliability High for inspected configuration and branches, Medium for operational implications; a second independent repository owner beyond baseline R1-R3.
- Exact configuration: `uv run $CLAUDE_PROJECT_DIR/.claude/hooks/stop.py --chat`.
- Exact implementation excerpts: `log_data.append(input_data)`; `json.dump(log_data, f, indent=2)`; `if args.notify: announce_completion()`; `timeout=10`.
- Config-to-handler trace: the configured Stop handler stores raw event data in `logs/stop.json` and, with `--chat`, converts transcript JSONL into `logs/chat.json`. The inspected configured Stop command does NOT pass `--notify`; its optional LLM/TTS branches must not be described as enabled by that Stop configuration. Other configured notification handlers were not inspected here.
- Optional branch: `get_llm_completion_message` tries OpenAI, Anthropic, and Ollama through subprocess helpers, with per-attempt ten-second limits, before predefined fallback messages; `announce_completion` invokes a selected TTS helper with another ten-second limit. Provider internals and actual data transmission were not inspected or exercised.
- Failure handling: malformed input and generic exceptions exit zero. Optional TTS failures are swallowed. This is observational availability, not fail-closed enforcement.
- Local inferences, not reproduced defects: the read-append-rewrite log sequence has no explicit lock or atomic rename in the inspected function; concurrent invocations could lose updates. Copying transcript content creates another retention surface. Sequential optional service timeouts can accumulate; no end-to-end latency measurement is claimed.
- Test definitions for this Python handler were not inspected. Capturing `stop_hook_active` does not itself establish loop protection; the inspected handler does not return a blocking decision.

### SI08 - Rejected Test-Path Lead

- Opened URL: https://raw.githubusercontent.com/backnotprop/plannotator/main/apps/hook/hooks/mod/plan.test.ts
- Type: failed retrieval; browser displayed `404: Not Found`.
- Disposition: no claims about module test coverage are derived from this guessed path. SI06 is an actually opened neighboring test file, not a substitute assertion that SI04 is tested.

## Updated Coverage and Provisional Portability

Two newly inspected independent owners: `backnotprop` and `disler`. These count as maintainers/examples, not deployments. Two implementation traces have direct code evidence: Plannotator plan resolution plus tests, and configured Stop telemetry plus handler. The approval-state module adds a third implementation slice but not a third configuration-to-test trace. The target of three complete implementation/test traces remains unmet.

| Pattern | Candidate mapping and required evidence | Portability limit / test still needed |
| --- | --- | --- |
| Plan-hash approval, SI04 | Claude plan-tool/permission boundary; Copilot would need an equivalent review workflow, session state, hash comparison, and approved-content delivery. Compare SH07.1, SH09.1, SH10.1 in [host evidence](supplementary-hosts-and-alternatives.md). | No equivalent Copilot `ExitPlanMode` tool is established here. Test changed-plan approval invalidation, duplicate callbacks, concurrent sessions, permission rechecks, and old-plugin/new-CLI combinations before claiming a port. |
| Safe fresh-plan resolution, SI05-SI06 | Portable helper logic if the receiving host supplies a trustworthy plan-file location; no lifecycle-name equivalence required. | Tests must cover Windows path conventions, file replacement races, fallback freshness, regular files, and the actual host payload. Static code is not a security-boundary proof. |
| Telemetry and completion notification, SI07 | Candidate Stop/agentStop and notification events; schema adapter plus per-session storage. | Use the exact host events/contracts in the separate host evidence. Test redaction, retention, concurrent writes, callback duplication, timeout budget, and offline behavior. No provider or runtime compatibility is established. |

These mappings are provisional analysis for the supervising agent, not implementation recommendations or merged conclusions. No baseline claim or H1-H8 verdict is replaced.

## Reopened Code Line Locators

The parent extracted these one-based source lines from raw browser page text on 2026-10-17. They are precise within the inspected moving branches, not immutable permalinks:

| Evidence | External repository path | Inspected source lines |
| --- | --- | --- |
| SI04 | `backnotprop/plannotator:apps/hook/hooks/mod/plan.ts` | 23 (normalization function), 24 (trailing whitespace removal), 54 (hash match), 66 (approved plan delivery). |
| SI05 | `backnotprop/plannotator:apps/hook/server/claude-plan.ts` | 36 (absolute Markdown path), 41 (regular file and size), 42 (file or inline fallback). |
| SI06 | `backnotprop/plannotator:apps/hook/server/claude-plan.test.ts` | 34 (stale-snapshot test), 81 (size-cap test), 86 (FIFO test and Windows skip). |
| SI07 | `disler/claude-code-hooks-mastery:.claude/settings.json` | 58 (Stop event), 64 (Stop command with `--chat` only). |
| SI07 | `disler/claude-code-hooks-mastery:.claude/hooks/stop.py` | 87, 104, 120, 146 (individual timeouts); 188 and 192 (append/rewrite logging); 211 (transcript-copy path); 218 (optional notification); 221, 225, 228 (zero exits). |

Source URLs are recorded in SI04-SI07 above. See [supplementary verification](supplementary-verification.md) for independent-review limitations and parent source rechecks.