---
name: context-distiller
description: >
  Aggressively distills domain concepts into shared abstractions and specific
  bounded contexts. Uses verb-first, effect-first analysis and mandatory user
  questions to confirm boundaries. English only.
argument-hint: "[domain description, event storming output, or concept list]"
---

# Context Distiller

Use this skill when you need to decide whether concepts should be generalized,
split into separate contexts, or kept specific.

## Default contract

- **Language**: English only.
- **Mode**: interactive by default.
- **Questioning**: ask the user before final boundary decisions.
- **Output**: produce a distilled context map, not implementation guidance.
- **Style**: aggressive about finding shared behavior, skeptical about merges.

## When to use

- You have a domain block, event storming output, or concept list.
- Several concepts look similar and you want to know whether they can share a model.
- The same noun appears in multiple contexts with different behavior.
- You suspect a context is too broad and should be split.
- You want to discover reusable generalized capabilities such as availability, capacity, or scheduling.

## When not to use

- You only need code implementation advice.
- The domain is already clear and has no boundary ambiguity.
- You only need a quick lookup of a known concept.

## Aggressive rules

1. **Search by verbs, not nouns.**
   - If two concepts share the same action, treat them as a generalization candidate.
   - If only one concept appears, still test whether the same verbs apply to other missing concepts.

2. **Test by consumer effect, not source cause.**
   - Different causes can still produce the same effect for the consuming context.
   - If the consumer only cares about the effect, keep the source details out of the generalized model.

3. **Split by meaning, not by spelling.**
   - If the same word carries different data, commands, or responsibilities in different contexts, split it.

4. **Attack every generalization with counterexamples.**
   - Ask what would break the abstraction.
   - If the generalized model must know type-specific details, the boundary is wrong or incomplete.

5. **Propose extra concepts.**
   - For every confirmed generalization, suggest 2-4 additional concepts that were not mentioned explicitly.
   - Mark them as speculative.

## Workflow

### 1. Extract nouns and verbs

Build two inventories:

- **Nouns**: domain concepts, actors, resources, roles.
- **Verbs**: commands, actions, state changes, checks.

Do not interpret yet.

### 2. Find split candidates

For each noun used in multiple contexts, ask:

> Does this word mean the same thing everywhere?

Split it when:

- different contexts need different data
- different commands apply
- different actors treat it differently

### 3. Find generalization candidates

For each noun group, ask:

> In this context, do these different things behave identically?

Generalize it when:

- the same verbs apply
- the same questions are asked
- the same consumer effect matters
- substitution does not break the context

### 4. Probe missing concepts

For each generalization, ask:

> What other concepts could behave the same way even if they were not mentioned?

List 2-4 speculative candidates.

### 5. Ask the user

Before finalizing boundaries, ask clarifying questions.

- Ask **one question at a time**.
- Prefer the host question UI:
  - VS Code: `vscode_askQuestions`
  - Copilot CLI: interactive questions / chat question flow
- If no host question UI is available, ask in plain chat.
- Always include **"To zalezy / It depends"** as the last option when options are offered.
- Stop after each question and wait for the answer.

Use questions like:

- "Do these two concepts need separate models, or do they only differ by source process?"
- "Does the consumer care why this changed, or only that the effect happened?"
- "If a new concept appeared tomorrow, would the current abstraction still cover it?"

### 6. Produce the distillation

Return:

- ambiguities
- generalizations
- speculative extra concepts
- distilled context map
- open questions

## Output format

```markdown
# Context Distillation: [Domain Name]

## Ambiguities

| Word | Context A | Meaning A | Context B | Meaning B | Resolution |
|---|---|---|---|---|---|

## Generalizations

| Words | Shared behavior | Generalized as | Technique |
|---|---|---|---|

## Speculative extra concepts

| Generalization | Proposed concept | Why it fits | Status |
|---|---|---|---|

## Distilled context map

### [Context name]

**Key question**: "[single question this context answers]"

**Generalized concepts**:
| Original concepts | Generalized as | Kept | Dropped |
|---|---|---|---|

**Specific concepts**:
- ...

**What this context does not know**:
- ...

## Open questions

- ...
```

## Hard rules

- Never silently assume boundary-affecting details.
- Never skip the consumer-effect test.
- Never skip counterexamples.
- Never drift into implementation advice.
- Never create a global model when the scope is local.
- If the model is not needed, say so plainly.
