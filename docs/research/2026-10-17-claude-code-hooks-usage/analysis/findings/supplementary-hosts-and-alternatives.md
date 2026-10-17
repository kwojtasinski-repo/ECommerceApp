# Supplementary Evidence: Hosts and Alternatives

Date accessed: 2026-10-17. Status: evidence handoff only; final synthesis gate CLOSED.

The current request approves additive external gathering and provisional portability analysis, not merged conclusions or recommendations. This file is the only output of this pass. No baseline, configuration, hook implementation, or other supplementary file is changed.

## Scope and Method

- Approved inputs read: [supplementary research plan](../../planning/supplementary-research-plan.md), [original research plan](../../planning/research-plan.md), [source manifest](../../planning/sources.md), [baseline official evidence](external-official-docs.md), and [baseline verification](99-verification.md). The supplementary approval extends the original manifest's restrictions on pre-commit and other agents.
- Exactly ten distinct substantive official pages were opened through the integrated browser. SH01-SH10 identify sources; dotted IDs identify 44 individual evidence entries. All quotes below are short exact excerpts from inspected page text, not search snippets.
- Reliability is High for what the official documentation states, not for installed behavior. No agent, Git hook, CI job, downloaded script, or adversarial bypass was executed. No source revision was pinned to a commit.
- Host names are explicit: Claude Code; VS Code Local harness; VS Code Copilot harness; Copilot CLI; Copilot cloud agent; Gemini CLI. A shared file format or similarly named event is not proof of equivalent execution.
- The supervisor reported two RAG connection refusals and a bounded direct correction fallback. No additional RAG lookup was made. The local research-gatherer skill and relevant instructions were read directly.

## SH01 - Git Hook Boundaries

Opened URL: https://git-scm.com/docs/githooks

Access date: 2026-10-17. Source type: official Git reference. Reliability: High, documentation only. Revision: page says "githooks last updated in 2.54.0"; moving documentation, no commit pin.

| Evidence ID | Exact quote or excerpt | Documented boundary |
| --- | --- | --- |
| SH01.1 | "the executable bit set are ignored."; "By default the hooks directory is $GIT_DIR/hooks, but that can be changed via the core.hooksPath configuration variable" | The first excerpt is the end of the sentence saying hooks without the executable bit are ignored. Hook discovery and executability are prerequisites; a repository file alone is not proof of activation. |
| SH01.2 | "This hook is invoked by git-commit[1], and can be bypassed with the --no-verify option."; "Exiting with a non-zero status from this script causes the git commit command to abort before creating a commit." | `pre-commit` gates commit creation, not earlier agent edits or model/tool events. The separately inspected `commit-msg` section also permits `--no-verify`. |
| SH01.3 | "Just before starting to update refs on the remote repository, the pre-receive hook is invoked."; "If the hook exits with non-zero status, none of the refs will be updated." | `pre-receive` is a receiving-repository boundary, not a local pre-edit boundary. This source does not establish that arbitrary GitHub.com repositories permit installing receiver-side scripts. |

## SH02 - pre-commit Framework and CI

Opened URL: https://pre-commit.com/

Access date: 2026-10-17. Source type: official framework documentation. Reliability: High, documentation only. Revision: unpinned live page; no document revision established. Embedded version examples are not treated as the current installed release.

| Evidence ID | Exact quote or excerpt | Documented boundary |
| --- | --- | --- |
| SH02.1 | "The SKIP environment variable is a comma separated list of hook ids."; `SKIP=flake8 git commit -m "foo"` | Selective skipping is an intentional interface, in addition to the Git bypass in SH01.2. |
| SH02.2 | "pre-commit can also be used as a tool for continuous integration."; "adding pre-commit run --all-files as a CI step"; `pre-commit run --from-ref origin/HEAD --to-ref HEAD` | The same check runner can execute in CI independently of local hook installation; all-files and changed-ref scopes are distinct. A CI invocation does not itself establish a required merge gate; see SH03. |
| SH02.3 | "Providers of hooks can select which git hooks they run on by setting the stages property"; "if true this hook will execute using a single process instead of in parallel." | Event selection uses `stages`; the second quote documents `require_serial`. This process setting is not an agent session/context API or a general cross-process state lock. |

## SH03 - GitHub Merge Gate and Bypasses

Opened URL: https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches

Access date: 2026-10-17. Source type: official GitHub repository-policy documentation. Reliability: High, documentation only. Revision: unpinned live page; no revision date established.

| Evidence ID | Exact quote or excerpt | Documented boundary |
| --- | --- | --- |
| SH03.1 | "By default, the restrictions of a branch protection rule don't apply to people with admin permissions to the repository or custom roles with the \"bypass branch protections\" permission." | Branch protection has explicit default bypass actors. The page also lists "Do not allow bypassing the above settings". |
| SH03.2 | "Required status checks must have a successful, skipped, or neutral status before collaborators can make changes to a protected branch." | An accepted status is not evidence that every intended test ran. This quote does not resolve the separate skipped-workflow versus skipped-job behavior. |
| SH03.3 | "you can select an app that has recently set this check as the expected source of status updates. If the status is set by any other person or integration, merging won't be allowed." | Required-check provenance can be constrained. No repository settings or actual check producer were inspected in this pass. |

## SH04 - Gemini CLI Overview

Opened URL: https://geminicli.com/docs/hooks/

Access date: 2026-10-17. Source type: official Google Gemini CLI documentation. Reliability: High, documentation only. Revision: "Last updated: Apr 13, 2026"; no release or commit pin.

| Evidence ID | Exact quote or excerpt | Documented boundary |
| --- | --- | --- |
| SH04.1 | "Hooks run synchronously as part of the agent loop"; "Gemini CLI waits for all matching hooks to complete before continuing." | General overview wording; the explicit `PreCompress` and `SessionEnd` exceptions in SH05.7 qualify it. |
| SH04.2 | "Gemini CLI fingerprints project hooks."; "you will be warned before it executes." | The warning applies to a changed hook name or command. A warning/fingerprint description is not evidence of mandatory interactive approval or hashing the contents of a referenced script. See SH06.1. |
| SH04.3 | "/hooks enable-all or /hooks disable-all"; "/hooks enable <name> or /hooks disable <name>" | Individual and global operational controls are documented. They are not evidence of an unbypassable policy boundary. |

## SH05 - Gemini CLI JSON and Lifecycle Contracts

Opened URL: https://geminicli.com/docs/hooks/reference/

Access date: 2026-10-17. Source type: official technical reference. Reliability: High, documentation only. Revision: "Last updated: Apr 10, 2026"; no release or commit pin. Current reference says only `type: "command"` is supported, default timeout is 60000 milliseconds, and group `sequential` selects serial versus parallel execution.

| Evidence ID | Exact quote or excerpt | Documented boundary |
| --- | --- | --- |
| SH05.1 | "hookSpecificOutput.tool_input: An object that merges with and overrides" | `BeforeTool` receives `tool_name` and `tool_input`; the quoted field rewrites model arguments before execution. It supports denial plus `reason` as agent-visible tool-error feedback. Merge semantics must not be presumed identical to a full replacement in another host. |
| SH05.2 | "decision: Set to \"deny\" to hide the real tool output from the agent."; "hookSpecificOutput.tailToolCallRequest: ({ name: string, args: object }) A request to execute another tool immediately after this one." | `AfterTool` can replace a denied result with `reason`, add `additionalContext`, or request a tail call whose result replaces the original response. The original tool has already executed. |
| SH05.3 | "hookSpecificOutput.llm_request: An object that overrides parts of the outgoing request"; "the CLI skips the LLM call entirely and uses this as the response." | `BeforeModel` accepts a stable `llm_request` and can return a synthetic `hookSpecificOutput.llm_response`; broader than tool-argument or prompt-text rewriting. |
| SH05.4 | "hookSpecificOutput.llm_response: An object that replaces"; "Fired for every chunk generated by the model. Modifying the response only affects the current chunk." | `AfterModel` operates on response chunks, not just final-turn text. Cross-chunk filtering guarantees remain untested. |
| SH05.5 | "\"NONE\": Disables all tools (Wins over other hooks)."; "Union Strategy:"; "whitelists are combined." | `BeforeToolSelection` returns `toolConfig.mode` and `allowedFunctionNames`; multiple-hook whitelist union is not intersection. The reference says `decision`, `continue`, and `systemMessage` are unsupported for this event. |
| SH05.6 | "prompt_response: (string) The final text generated by the agent."; "reason: Required if denied. This text is sent to the agent as a new prompt to request a correction." | `AfterAgent` exposes final response and `stop_hook_active`; `decision: "deny"` retries, while `continue: false` stops without retry. This is not the same boundary as per-chunk redaction. |
| SH05.7 | "Advisory Only: Fired asynchronously. It cannot block or modify the compression process."; "The CLI will not wait for this hook to complete" | First quote: `PreCompress`, suitable only for advisory logging/state saving per the reference. Second: best-effort `SessionEnd`. `SessionStart.source` lists startup, resume, clear; its `additionalContext` is first-turn history interactively or prepended to the prompt non-interactively. No post-compression event or `compact` startup source is documented in this reference. |

## SH06 - Gemini Trust and State Practices

Opened URL: https://geminicli.com/docs/hooks/best-practices/

Access date: 2026-10-17. Source type: official guidance with example code. Reliability: High for the documented pattern, not for security guarantees. Revision: "Last updated: Apr 10, 2026"; no release or commit pin.

| Evidence ID | Exact quote or code excerpt | Documented boundary |
| --- | --- | --- |
| SH06.1 | "A unique identity is generated for each hook based on its name and command."; "The hook is executed (unless specific security settings block it)." | The documented sequence is detection, identity, warning, execution, trust. It does not describe a mandatory approval dialog. No claim that changing script bytes without changing the command renews trust is supported here. |
| SH06.2 | "Hooks run as your user. They can do anything you can do (delete files, install software)." | Hook privileges are a security surface. No sandbox or secret-redaction guarantee was demonstrated. |
| SH06.3 | "Store results between invocations to avoid repeated computation"; `const CACHE_FILE = '.gemini/hook-cache.json';`; `fs.writeFileSync(CACHE_FILE, JSON.stringify(data, null, 2));` | The inspected caching example uses a shared file; the inspected excerpt contains no lock or atomic rename. A concurrency risk is an inference, not a reproduced defect. Combine cautiously with SH05's parallel option. |

## SH07 - Reopened Claude Code Reference

Opened URL: https://code.claude.com/docs/en/hooks

Access date: 2026-10-17. Source type: official Anthropic reference. Reliability: High, documentation only. Revision: unpinned live page; feature gates include `onFailure` requiring v2.1.295 or later. No installed Claude version was probed.

| Evidence ID | Exact quote or code excerpt | Documented boundary |
| --- | --- | --- |
| SH07.1 | "PreToolUse: updatedInput directly under hookSpecificOutput"; "before it runs." | Tool-input replacement before execution is documented, not only allow/deny. The exact output key is `hookSpecificOutput.updatedInput`. |
| SH07.2 | "updatedToolOutput"; "with the provided value before it is sent to Claude."; "Prefer updatedToolOutput, which works for all tools" | `PostToolUse` replacement must match the tool output shape and is broader than the older `updatedMCPToolOutput`. The reference explicitly says this does not undo tool side effects. |
| SH07.3 | "MessageDisplay is display-only"; "The transcript and what Claude sees keep the original text"; "displayContent" | `MessageDisplay` receives display `delta` and can return `hookSpecificOutput.displayContent`. It cannot block the message or alter model-visible history. Failure/timeout displays the original text; it is not Gemini `AfterModel` response replacement. |
| SH07.4 | "The last_assistant_message field contains the text content"; "after stop hooks have continued the turn eight times in a row, Claude Code overrides the next block and ends the turn." | `Stop` can inspect final response text and block with a reason; `stop_hook_active` identifies continuation. The documented cap resets on tool calls and can be raised with `CLAUDE_CODE_STOP_HOOK_BLOCK_CAP`; do not treat eight as a universal safety proof. |
| SH07.5 | "\"compact\" after compaction" | `SessionStart.source` explicitly includes `compact`, unlike SH05.7 and SH09.3. The reference's event list also includes `PostCompact`; no assumption is made that its output contract equals `SessionStart`. |
| SH07.6 | "All matching hooks run in parallel." | Do not assume ordered shared-state updates or a Gemini `sequential` setting when porting handlers. This re-confirms baseline D1.2. |
| SH07.7 | "it only injects additionalContext alongside it"; "a PreToolUse hook matching the Skill tool fires only when Claude calls the tool, but typing /skillname directly bypasses PreToolUse." | First excerpt concerns `UserPromptSubmit`, which cannot replace the prompt. The latter is the `UserPromptExpansion` documentation: direct user command expansion is a distinct path, with block/context output. It is not a generic model-request replacement API. |

## SH08 - Reopened VS Code Harness Routing

Opened URL: https://code.visualstudio.com/docs/copilot/customization/hooks

Resolved URL: https://code.visualstudio.com/docs/agent-customization/hooks

Access date: 2026-10-17. Source type: official Microsoft documentation, Preview. Reliability: High, documentation only. Revision: page displays "10/14/2026" at "Edit this page"; no VS Code build or commit pin.

| Evidence ID | Exact quote | Documented boundary |
| --- | --- | --- |
| SH08.1 | "This file compatibility does not make their behavior identical."; "Copilot harness sessions use the same SDK hook implementation as Copilot CLI."; "verify that the event is available in the selected VS Code version." | VS Code Copilot harness candidates use SH10 contracts, subject to build availability; Local harness uses SH09. VS Code Claude harness uses Claude Agent SDK, not Local payloads. |
| SH08.2 | "Local parses nested commands but ignores matcher values, so every command for the event runs."; "Workspace hook files are subject to Workspace Trust." | Claude-format discovery in Local requires `chat.useClaudeHooks` (off by default); `chat.useHooks` controls Local execution and is on by default. Re-confirms baseline D4.3-D4.4. |
| SH08.3 | "Require approval before an agent edits a script that a later hook can execute." | This is vendor security guidance, not proof that the host automatically gates every such script edit. No script-edit protection was tested. |

## SH09 - Reopened VS Code Local Contracts

Opened URL: https://code.visualstudio.com/docs/agents/reference/hooks-reference

Access date: 2026-10-17. Source type: official Microsoft Local harness reference. Reliability: High, documentation only. Revision: page displays "10/14/2026" at "Edit this page"; no build or commit pin.

| Evidence ID | Exact quote or excerpt | Documented boundary |
| --- | --- | --- |
| SH09.1 | "updatedInput"; "Optional replacement tool input. The value must match the tool schema for the Local harness session."; "If updatedInput does not match the tool schema for the Local harness session, the harness ignores it." | `PreToolUse` receives `tool_name`/`tool_input`; returns `hookSpecificOutput.updatedInput`. Schema-mismatched rewrites are not enforcement. |
| SH09.2 | "The hook can add context or block further processing"; "Reason provided to the model when processing is blocked." | `PostToolUse` documents `decision: "block"`, `reason`, and `hookSpecificOutput.additionalContext`, not a replacement-result field in the inspected contract. This is not proof that it can hide the original result. |
| SH09.3 | "The SessionStart hook fires when the first prompt starts the session."; "Currently always \"new\"." | `SessionStart` can add `hookSpecificOutput.additionalContext`; its source is not Claude's `compact`. `PreCompact` input documents `trigger: "auto"` and supports common output, not a documented summary replacement/post-compact reinjection contract. |
| SH09.4 | "true when the agent is already continuing because a previous Stop hook blocked it."; "Prevents the agent execution from stopping." | `Stop` has `stop_hook_active`, with `hookSpecificOutput.decision: "block"` and `reason`. The inspected input does not document a final-response field equivalent to Gemini `prompt_response` or Claude `last_assistant_message`. Baseline D5.4 warns the transcript is not a stable hook API. |

## SH10 - Reopened Copilot CLI and Cloud Contracts

Opened URL: https://docs.github.com/en/copilot/reference/hooks-reference

Access date: 2026-10-17. Source type: official GitHub reference covering CLI and cloud separately. Reliability: High, documentation only. Revision: unpinned live page; no package version, release date, or commit established. VS Code Copilot harness applicability is conditional on SH08.1, not assumed for Local.

| Evidence ID | Exact quote or code excerpt | Documented boundary |
| --- | --- | --- |
| SH10.1 | "modifiedArgs"; "Substitute tool arguments to use instead of the originals." | Native `preToolUse` input is `toolName`/`toolArgs`; output is top-level `modifiedArgs` plus optional permission verdict/reason. PascalCase `PreToolUse` switches input to snake_case and maps runtime names to Claude names; do not copy Local's `updatedInput` output key. |
| SH10.2 | "modifiedResult is honored by both SDK programmatic hooks and command/HTTP config-file postToolUse hooks."; "Additional guidance appended to textResultForLlm" | `postToolUse` returns `modifiedResult: { resultType: "success", textResultForLlm: string }` and/or `additionalContext`. Multiple additional-context results are joined and capped at 10 KB. This support must not be inferred from `userPromptSubmitted`, whose config-file output is dropped (baseline D6.9, re-observed here). |
| SH10.3 | "modifiedTransformedPrompt?: string; // Replaces the model-facing content"; "the prompt displayed in the timeline is unaffected" | `userPromptTransformed` receives `prompt` and `transformedPrompt`; replacement affects model-facing content and persisted history and is replayed on resume. It cannot block/handle the turn. The event table says it fires in cloud. This is not the SDK-only `userPromptSubmitted.modifiedPrompt` interface. |
| SH10.4 | "modifiedResponse"; "subagentStop only."; "Every hook receives the same original response, and the last hook to return modifiedResponse wins" | `agentStop`/`subagentStop` support `decision: "block"` and `reason` continuation; only `subagentStop` can replace the response returned to the parent. Rewriters do not compose; block wins over replacement. Not a general primary-model chunk filter. |
| SH10.5 | "If multiple hooks of the same type are configured, they execute in order." | Order does not imply transformed-output chaining, as SH10.4 explicitly qualifies. No state locking guarantee was established. |
| SH10.6 | "Fully non-interactive."; "The agent runs with all tool permissions pre-granted"; "Only the bash field on command hooks is honored; powershell entries are ignored." | Cloud has Linux/ephemeral storage and no permission dialogs; `command` is a fallback. A `preToolUse` verdict of `ask` becomes `deny`. CLI interactive approval patterns must not be transferred unchanged. |
| SH10.7 | "Command hook timeouts are always fail-open, even for preToolUse and admin-deployed policy hooks"; "HTTP preToolUse hooks are fail-open" | Command `preToolUse` crashes/non-timeout nonzero exits deny; timeouts do not. Re-confirms baseline D6.3 rather than weakening it to a generic fail-closed label. |
| SH10.8 | "Only additionalContext is consumed for sessionStart (command and HTTP variants)."; "notification only." | `sessionStart` context injection is documented, with source values startup, resume, new. The last excerpt concerns `preCompact`: notification-only, with manual/auto triggers in CLI and only auto in cloud. No `compact` startup source or post-compaction output contract was established. |

## Parallel Findings Against the Baseline

These are comparisons of cited evidence, not replacements for baseline records. No baseline claim has been silently corrected.

| Baseline ID | New evidence | Comparison status and limit |
| --- | --- | --- |
| V4 / H8: CI-before-hook ordering not verified | SH01.2-SH03.3 | Newly documented separate boundaries: before local commit, in CI, and before protected-branch updates/merge. No source proves a universal sequence of preferred mechanisms or a requirement to adopt them in that order. |
| D1.1 and D1.11 | SH07.3-SH07.5 | Added output contracts: display-only transformation versus final-text validation versus compaction context. `MessageDisplay` was already listed; merely listing it does not establish model-visible redaction. The cap is configurable and resets on tool calls, qualifying any reading of D1.11 as an immutable global cap. |
| D5.2 and V3 | SH09.1-SH09.4 | Additive, not contradictory: Local supports schema-checked input replacement, but its documented startup source is only `new`. V3 is not a complete lifecycle/output equivalence matrix. |
| D6.9 | SH10.2-SH10.3 | Apparent conflict resolved by event identity: dropped `userPromptSubmitted` config-file output does not imply that all config-file mutation is unsupported. `userPromptTransformed.modifiedTransformedPrompt` and `postToolUse.modifiedResult` are distinct documented contracts. |
| D4.2-D4.4 and V3's column "VS Code Local" | SH08.1-SH08.2 | Confirmed for Local; do not generalize that column to every VS Code session. The Copilot harness routes to SDK/CLI contracts with version-availability checks. |
| D1.2 versus potential Gemini portability | SH05 configuration and SH07.6 / SH10.5 | Different scheduling contracts: Gemini group serial/parallel selection, Claude matching hooks parallel, Copilot same-type hooks ordered. No shared-state portability conclusion follows. |
| Baseline source manifest F, marked not opened | SH01-SH06 | Previously absent source families now opened under explicit supplementary authorization. No second alternative agent was needed. |
| Gemini overview versus reference | SH04.1 and SH05.7 | Source-internal tension: broad synchronous wording has explicit asynchronous/best-effort exceptions. Preserve both claims; do not use the overview as a shutdown/compression durability guarantee. |

No newly reproduced runtime contradiction is claimed. The new primary sources mostly qualify omissions or over-broad interpretations rather than falsify the baseline's explicit narrow statements.

## Provisional Portability Inferences

INFERENCE ONLY: this table is an evidence handoff, not merged synthesis, recommendations, an adapter specification, or a claim that a port works. Required tests are unexecuted. Each row's candidates come from the cited contracts, not event-name similarity.

| Source pattern and citations | Required event/input/output | Claude Code candidate | Copilot candidates by host | Missing capability or mismatch; test still needed |
| --- | --- | --- | --- | --- |
| Final-output correction (SH05.6, SH07.4, SH09.4, SH10.4) | End of primary turn, final text, rejection reason, continuation flag | `Stop` with `last_assistant_message`, `stop_hook_active`, block/reason | Local `Stop` block/reason lacks documented final-text input; CLI/cloud `agentStop` can continue but does not replace primary response; VS Copilot harness conditional on SH08.1 | Not streaming redaction. Verify final-text availability, user-visible timing, bounded retries, and tool-call effects on loop caps in each installed host. |
| Tool-input rewrite (SH05.1, SH07.1, SH09.1, SH10.1) | Before execution, parsed arguments, rewritten schema-valid arguments | `PreToolUse` / `hookSpecificOutput.updatedInput` | Local: same named output key but Local schema; CLI/cloud: `preToolUse.modifiedArgs`; VS Copilot harness: CLI contract subject to version | Gemini documents merge/override; do not assume matching replacement/merge or approval re-check semantics. Use a harmless test tool to assert actual arguments and whether permission evaluation sees the rewrite. |
| Hide/redact tool result (SH05.2, SH07.2, SH09.2, SH10.2) | After tool completion, original result, replacement returned to model | `PostToolUse.updatedToolOutput`, matching tool output shape | CLI/cloud `postToolUse.modifiedResult`; Local documents context/block only; VS Copilot conditional | Replacement does not undo execution. Verify raw versus model-visible versus logged result, schema handling, and whether secrets remain in transcripts. Gemini tail-call chaining has no confirmed equivalent here. |
| Model interception (SH05.3-SH05.5, SH07.3, SH10.3-SH10.4) | Outgoing model request/toolset or incoming response chunks; request/response replacement | `MessageDisplay` is only a display candidate, explicitly NOT model/history replacement | CLI/cloud `userPromptTransformed` rewrites prompt content, not full model request; `subagentStop.modifiedResponse` rewrites only child return; no Local equivalent established | No equivalent full request/synthetic-response/chunk interception established in inspected Claude/Copilot contracts. Test intended privacy boundary, chunk-spanning content, history, and downstream tools before any portability claim. |
| Recovery/checkpoint injection (SH05.7, SH07.5, SH09.3, SH10.8) | Pre-compression save plus guaranteed post-compression/restart load into model context | `SessionStart.source=compact` plus context injection is documented | Local startup is `new`; CLI sources startup/resume/new plus context; cloud preCompact auto only; VS Copilot conditional | Gemini pre-compression is async; Copilot preCompact is notification-only. Test crash/compaction races and idempotent reload; do not map all startup events to `compact` or assume saved state was flushed. |
| Trust and approval (SH06.1-SH06.2, SH08.2-SH08.3, SH10.6-SH10.7; baseline D1.15) | Admission before executing changed hook code, privilege limits, reliable refusal | Baseline reports workspace trust and permission hooks; script-content renewal not established | Local Workspace Trust; CLI interactive ask differs from cloud ask-to-deny; cloud no dialogs | Gemini name/command identity is not verified script-content integrity. Test unchanged command with modified script bytes, non-interactive admission, timeout, and revocation; do not infer hard approval from a warning. |
| Parallel/shared state (SH02.3, SH05 configuration, SH06.3, SH07.6, SH10.4-SH10.5) | Session identity, ordering/aggregation, atomic shared updates | Parallel matching handlers require explicit coordination if they share state | CLI ordered hooks still do not compose response rewrites; Local ordering not established; cloud files are ephemeral | No inspected contract supplies cross-session locking. Test concurrent sessions/worktrees, lost updates, duplicate delivery, redactor-plus-formatter behavior, and cleanup after failure. |
| Local validation versus merge enforcement (SH01-SH03) | File/ref checks; then trusted required CI status at protected boundary | Not an agent lifecycle equivalent | Not a CLI, Local, or cloud lifecycle equivalent | Cannot prevent pre-commit edits, tool execution, or model-output disclosure. Test local bypass, missing installation, CI scope, accepted/skipped checks, status producer, and admin bypass using a disposable repository. |

## Rejected Leads and Unverified Surfaces

- A second alternative agent (Cursor/Codex) was not opened: Gemini already supplied the targeted additional patterns within the ten-page budget. No claims about those agents' capabilities are made.
- Independent implementation repositories in the supplementary plan belong to the separate independent-implementations deliverable. They were not silently treated as inspected evidence here. This pass does not satisfy that deliverable's implementation/test traces.
- The original plan's blog, issue, settings, and permissions families remain baseline work, not newly reopened pages. This file reopens the specific host references needed for its assigned gaps; it does not claim to rerun the entire original research plan.
- No installed versions, managed-policy settings, GitHub branch settings, permissions, environment variables, or repository hook behavior were probed. Documentation is not a runtime probe.
- No evidence was gathered for arbitrary GitHub.com server-side `pre-receive` installation, skipped-workflow semantics, model/tool secret removal from every storage surface, or lossless shutdown/checkpoint durability.
- Gemini hook identity based on name/command does not establish script-byte integrity. Gemini tool-whitelist union does not establish intersection-based least privilege. A shared-file cache example does not establish race safety. These are withheld guarantees, not demonstrated exploits.
- No event absence claim means a permanent product limitation. Missing mappings mean not established in these opened, unpinned contracts.

## Retrieval and Artifact Verification

- Substantive source failures: none; all ten official pages yielded inspected text. The VS Code customization URL redirected as recorded in SH08.
- Tooling limitations: the requested `tool_search` loader was not exposed. Already-exposed integrated browser tools were used directly; no raw `fetch_webpage`, retired context-mode, shell HTTP retrieval, or downloaded code execution was used.
- Recoverable retrieval problems: same-domain tab reuse initially paused opening the GitHub and VS Code references, resolved with separate tabs; the VS Code page had two `main` elements, resolved by selecting `main.docs-main-content`; one Local reference tab became unavailable, resolved by reopening the same URL. A later Gemini best-practices tab also became unavailable; previously inspected excerpts were retained and an unnecessary quote was removed rather than assumed. Oversized excerpts were re-extracted in bounded form before use.
- Gemini pages reported aborted analytics requests, but their substantive documentation rendered and was inspected. These telemetry errors are not source-content failures.
- Before writing, the target was confirmed absent and SHA-256 hashes were captured for all 13 pre-existing research files. Immediately after this creation, a focused read-only check must verify ten source records, 44 evidence rows, provenance fields, resolvable local links, the closed synthesis gate, and unchanged captured hashes. This is an artifact check, not an external runtime test; its result is reported in the task handoff rather than presumed here.
- Audit-only maintainer Workflow 11 scope decision: this is an additive research artifact, not a change to routing, architecture, agent instructions, or solution structure. No configuration cascade is authorized or performed; no router, solution, setup-state, summary, or baseline verification file is edited. No maintainer subagent invocation is claimed.
- Confidence: High for the quoted vendor contracts and explicit boundaries; provisional for cross-host mappings; unverified for actual installed behavior, concurrency, and security enforcement. Supervisor verification and synthesis approval remain required.

INFORMATION GATHERER: evidence captured
Awaiting next instruction or synthesis approval.