---
name: linguistic-boundary-verifier
description: Strict read-only verifier for linguistic boundaries between bounded contexts. Analyzes language.md files, detects foreign-language leaks in strings, events, and API calls, and reports type-specific fixes without modifying code.
argument-hint: "[module names to check, or 'all', or module name --pr for single-module new concept check]"
---

# Linguistic Boundary Verifier

Use this skill only when the repository has language.md files that define module purpose, vocabulary, and integration points.

## Hard rules

- Read-only only. Never edit files, create patches, or suggest direct code changes as an action.
- Do not guess missing module roles, directions, or vocabularies. If a language.md is missing or unclear, stop and report the gap.
- Use language.md files as the source of truth for module vocabulary and boundary direction.
- Treat internal vocabulary as private unless language.md explicitly marks it as published or shared.
- Prefer concrete evidence over interpretation. Every violation must cite the exact code location and the foreign term or call.
- If a finding is uncertain, label it as a false positive candidate or boundary question instead of forcing a conclusion.
- Always show the current-state ASCII diagram before the violation table.
- Always show a before/after ASCII diagram for each confirmed violation or grouped violation set.
- Always ask for user validation before moving from detection to fix proposals.
- Never propose a fix that changes code identity when the real issue is behavior; preserve behavior, remove leakage.
- Never widen scope silently. If the requested module set is ambiguous, stop and ask.

## When to use

1. Cross-module boundary check: analyze 2+ modules or `all`.
2. Single-module PR check: analyze one module with `--pr` and validate whether new concepts fit its linguistic space.

## Phase 1: Discover and parse

- Read the relevant language.md files first.
- Build a vocabulary inventory for each context: core terms, published terms, events, operations, imports, exports, aliases.
- Reconstruct the relationship graph only from integration point sections.
- Report the contexts found, relationship types, and vocabulary sizes.

If the module has both `Core Terms` and `Published API`, consumers may use only the published vocabulary.

## Phase 2: Detect violations

- Search for foreign terms from one context in the code of another context.
- Check string literals, enum names, map keys, JSON keys, class names, event handlers, API calls, and column names.
- Read surrounding code, at least 10 lines, to understand the behavior.
- Filter out primitives, infrastructure vocabulary, and terms explicitly shared by language.md.

Classify findings only as one of these:

- String leak from foreign context.
- Event consumed in foreign language.
- API call in wrong direction.
- Internal vocabulary used where published vocabulary is required.
- False positive candidate.

## Phase 3: Present violations

- Show an ASCII architecture diagram first.
- Mark every confirmed leak with `❌`.
- Then present a table with: #, type, term/call, location, source context, and observed behavior.
- Ask the user whether to proceed with fix proposals.

## Phase 4: Propose fixes

- For string leaks, propose a behavior generalization in the upstream language.
- For events, choose between ACL translation and reverse-to-command only after checking whether the publisher knows the exact next step.
- For API calls, first decide whether the direction is correct; if not, reverse the dependency. If direction is correct but vocabulary is wrong, fix vocabulary only.
- Never propose a more specific term in a more generic module.
- Every fix must include a before diagram and an after diagram.
- If the user says the upstream must know, flag a boundary question instead of forcing a fix.

## Phase 5: Report

Produce `linguistic-boundary-report.md` with these sections:

1. Executive summary.
2. Current-state diagram.
3. Context inventory.
4. Relationship map.
5. Violations with evidence and fixes.
6. Recommendations.

## Single-module PR mode

- Diff the PR and extract new class names, methods, string literals, event types, and API usage.
- Compare them against the module's language.md vocabulary.
- Flag new concepts only when they are inconsistent with the module's linguistic space, leak downstream language into an upstream module, or break an existing generalization.
- If the module is a generalization, be strict.
- If the module is an integrator, be strict only about leakage into its upstreams.

## Output discipline

- Use plain English for the report.
- Keep diagrams concise and readable.
- Do not output implementation code.
- Do not change repository files.