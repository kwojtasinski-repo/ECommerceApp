# Supplementary Verification and Supervisor Handoff

Date: 2026-10-17. Status: evidence gathering and bounded verification complete; coverage PARTIAL; final synthesis NOT performed.

## Files and Preservation

- [Supplementary plan](../../planning/supplementary-research-plan.md).
- [Independent implementation evidence](supplementary-independent-implementations.md): SI01-SI03 are local scope observations; SI04-SI07 are external implementation/test evidence; SI08 is a rejected 404 lead.
- [Official hosts and alternatives](supplementary-hosts-and-alternatives.md): SH01-SH10, ten opened official pages and 44 evidence entries, plus explicitly provisional portability mappings.
- All 12 pre-existing research files were fingerprinted with SHA-256 before supplementary gathering. Subsequent checks found them unchanged. None of Opus's findings, source IDs, hypothesis verdicts, recommendations, or summaries were rewritten.
- No local hooks/configuration were changed. No downloaded hooks, vendor agents, test suites from external repositories, or live CI workflows were executed.

## Review Method and Independence Limits

An independent information-gatherer-lite invocation reviewed both supplementary evidence files. It confirmed baseline hashes and identified missing source-line locators and a weakly supported phrase about hashing. Its external browser/tool-loader path was unavailable: it opened ZERO external pages. Its verdicts were externally unverified, not source-confirmed passes.

The parent recovered by reopening actual primary sources through loaded browser tools, extracting bounded excerpts and one-based raw-code line numbers. This gives direct source corroboration, NOT independent external replication and NOT runtime verification. The hashing phrase was narrowed; source-line locators were added to the implementation file. Moving branches remain unpinned.

## Source Recheck Register

All sources below were opened/reopened by the parent on 2026-10-17. Source type and result are explicit; exact quotes are source text, not runtime output.

| Check | Opened source | Matching excerpt or locator | Result and limit |
| --- | --- | --- | --- |
| SV01: approval tied to reviewed text | https://raw.githubusercontent.com/backnotprop/plannotator/main/apps/hook/hooks/mod/plan.ts | Line 54: `state.approval.hash === planHash`; line 66: `updatedInput: { ...input, plan: approval.plan }`. | PASS for inspected code. No caller tracing, cryptographic proof, race testing, or Copilot compatibility established. |
| SV02: safe plan reads and tested fallback | https://raw.githubusercontent.com/backnotprop/plannotator/main/apps/hook/server/claude-plan.ts ; https://raw.githubusercontent.com/backnotprop/plannotator/main/apps/hook/server/claude-plan.test.ts | Resolver lines 36, 41, 42 constrain reads and fall back. Test line 86: `test.skipIf(process.platform === "win32")`. | PASS for code/test definitions. FIFO test is skipped on Windows; no tests executed. |
| SV03: configured telemetry versus optional TTS | https://raw.githubusercontent.com/disler/claude-code-hooks-mastery/main/.claude/settings.json ; https://raw.githubusercontent.com/disler/claude-code-hooks-mastery/main/.claude/hooks/stop.py | Settings line 64: `stop.py --chat`; handler line 218: `if args.notify:`; lines 188/192 append and rewrite event log. | PASS for configuration/handler distinction. Optional external helpers were not inspected; no assertion that this configured Stop sends data to providers. Concurrency/privacy concerns remain inference. |
| SV04: model interception is broader than a tool hook | https://geminicli.com/docs/hooks/reference/ | BeforeModel: `the CLI skips the LLM call entirely and uses this as the response.` PreCompress: `Advisory Only: Fired asynchronously. It cannot block or modify the compression process. Flow-control fields are ignored.` | PASS for official documentation. Confirms the lifecycle exception, not installed behavior or an equivalent Claude/Copilot API. |
| SV05: result rewriting and cloud permission boundary | https://docs.github.com/en/copilot/reference/hooks-reference | `modifiedResult is honored by both SDK programmatic hooks and command/HTTP config-file postToolUse hooks.` Cloud table: `A decision of "ask" is treated as "deny" because no user is available to answer.` | PASS for official documentation. Directly supports SH10.2 and the previously paraphrased SH10.6 claim. Not a Local-harness or runtime guarantee. |

All five sampled checks are source-confirmed by the parent. Claims outside this bounded sample remain backed by the gathering agent's opened-source record, not independently reopened by this verifier. Documentation reliability is high for stated contracts; operational and security guarantees are unverified.

## Coverage, Rejections, and Contradictions

- Two additional independent repository owners were inspected: backnotprop and disler. This satisfies the additional-maintainer target, not an adoption/deployment target.
- Three implementation slices were read, but only two traced implementation paths and one directly associated test file were collected. The target of three complete configuration-to-test traces is NOT met.
- Official pre-commit/Git/branch-protection sources fill a previously unopened family. They demonstrate distinct boundaries and bypasses, not a universal order of preference for CI versus hooks.
- Gemini is the one alternative-agent family examined. Its model-request hooks, output handling, whitelist aggregation, and compression exceptions are comparison evidence; no port was implemented or proven worthwhile.
- Preserve the SH04 overview versus SH05 asynchronous exception as a documented qualification. Do not flatten all hooks into synchronous lifecycle steps.
- Preserve Local versus Copilot versus Claude VS Code harness distinctions. A file-format match or event-name match is not execution equivalence.
- SI08 returned 404. No test-absence claim follows. No source count includes that failed response as substantive evidence.
- RAG refused two attempts; prior corrections used bounded direct-file fallback. One gatherer and the independent reviewer lacked their required tool loader. The parent recovered the implementation evidence and sampled external verification, with independence limits stated above.

## Artifact Checks and Remaining Gates

Focused checks validate required sections/IDs, local Markdown link targets, external URL syntax, and unchanged baseline hashes. They cannot prove website truth, installed tool behavior, race safety, actual hook activation, redaction completeness, or portability.

No .NET restore/build/test is appropriate: only additive Markdown research artifacts changed. No application behavior or project dependencies changed.

Required final close-out: invoke copilot-setup-maintainer Workflow 11 in audit-only mode. No configuration cascade is authorized. This evidence addition does not itself change architecture, routing, agent behavior, inventory, or solution structure; report any necessary follow-up rather than editing those files.

## Approval Boundary

The supervising agent should assess the separate SI/SH evidence against Opus's unchanged baseline, resolve any version-specific uncertainty, and decide which patterns merit further work. This file is a verification record, not a merged research summary, revised H1-H8 verdict set, or recommendation to adopt a hook.

RESEARCH GATHERER: supplementary evidence ready
Awaiting supervising-agent review; final synthesis requires human approval: APPROVE / REVISE / ABORT.