---
name: metaprogram-classifier
description: Identify active NLP metaprograms in utterances, written communication, or described behavior, then suggest communication strategies matched to the person's cognitive filters.
argument-hint: "[utterance, email text, transcript excerpt, or behavior description]"
---

# Metaprogram Classifier

## Enforcement Rules

- ALWAYS treat metaprograms as context-dependent filters, not fixed traits.
- NEVER use this skill for hiring, performance evaluation, or psychometric labeling.
- ALWAYS qualify findings with context and role.
- ALWAYS explain which evidence triggered each metaprogram.
- NEVER overstate certainty when the role may be masking the signal.
- ALWAYS suggest communication strategies that fit the detected filters.

Analyze utterances, written communication, or described behavior to identify active NLP metaprograms and suggest communication strategies adapted to the person's cognitive patterns.

**Output goal**: identify the strongest metaprogram signals, explain the evidence, and give concrete communication tactics.

## When to Use

- The user asks how to respond to a person
- The user wants to understand a recurring communication pattern
- The user wants to prepare for a difficult conversation
- The user wants communication advice for a specific person or role

## When NOT to Use

- Personality typing
- Hiring or performance evaluation
- Permanent labels like "this person is always detail-oriented"

---

## The 7 Metaprograms

### MP1: Information Sorting

- Similarities
- Differences

### MP2: Granularity

- Detail
- Big Picture

### MP3: Source of Authority

- Internal Reference
- External Reference

### MP4: World Orientation

- Away From Problems
- Toward Goals

### MP5: Self-Motivation

- Reactive
- Proactive

### MP6: Self-Persuasion

- Necessity
- Possibility

### MP7: Priority

- Self
- Others

---

## Workflow

### Step 0: Input acquisition

- Use the given text if present
- Otherwise scan the conversation
- If nothing is available, ask for the utterance or behavior to analyze

### Step 1: Detect signals

Identify metaprogram markers, compound patterns, and role-based distortions.

### Step 2: Qualify by context

Explain whether the signal is likely a stable filter or a role activation.

### Step 3: Suggest communication strategy

Give concrete tactics that match the detected filters.

### Step 4: Report

---

## Output Format

```markdown
# Metaprogram Classification

## Context
- **Subject**: [person or role]
- **Role context**: [if known]
- **Evidence source**: [utterance / transcript / behavior]

## Detected metaprograms

| Metaprogram | Pole | Confidence | Evidence |
|-------------|------|------------|----------|
| ... | ... | High / Medium / Low | quote |

## Compound patterns
- [pattern]

## Context caveats
- [role-based distortion or uncertainty]

## Communication strategy
- [do]
- [avoid]
```

---

## Hard Rules

- Do not diagnose personality.
- Do not ignore context.
- Do not ignore role masking.
- Do not give manipulation advice.
