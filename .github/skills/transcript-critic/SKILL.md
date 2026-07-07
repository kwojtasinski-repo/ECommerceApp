---
name: transcript-critic
description: >
	Critiques meeting transcripts for hidden decisions, false consensus, marginalized voices,
	scope drift, and unsupported claims. Use only on explicit request.
argument-hint: "[meeting transcript or discussion notes]"
---

# Transcript Critic

This skill critiques decision-making visible in transcripts.
It does not summarize, rewrite, or decide.

## Hard rules

- MUST analyze only when the user explicitly asks for critique, review, or diagnosis.
- MUST read the whole transcript before reporting anything.
- MUST quote the exact transcript line that triggered each finding.
- MUST classify important claims as fact, opinion, hearsay, or declarative conclusion.
- MUST treat opinion-to-fact escalation as a high-risk pattern.
- MUST distinguish genuine agreement from compliance and from not being asked.
- MUST flag silence only when there is a concrete decision risk.
- MUST generate diagnostic questions for the next meeting.
- MUST not summarize the transcript.
- MUST not infer intent, tone, or motive without evidence.
- MUST not make decisions for the user.

## When to use

- After a meeting where decisions were made.
- Before acting on meeting notes.
- When preparing questions for a follow-up meeting.
- When reviewing notes for missing decision logic.

## When not to use

- The user wants a summary.
- The user wants a rewrite of the transcript.
- The user wants a decision instead of a critique.

## Analysis framework

Run all seven checks on the transcript. Each check produces findings independently.

### Check 1: Fact vs Opinion vs Hearsay

For every claim made by a participant, classify it as:

- **Fact** — verifiable, with evidence in the transcript.
- **Opinion** — stated without evidence, based on experience or feeling.
- **Hearsay** — information from a third party, not verified.
- **Declarative conclusion** — stated as if it were fact, but without support.

Track when an opinion or hearsay claim gets treated as fact later.
For each finding, note who said it, the original classification, whether it escalated, and what verification would look like.

### Check 2: Consensus audit

When the conversation reaches a decision point, verify:

- Who explicitly agreed.
- Who was asked and said OK after being overruled or interrupted.
- Who was never asked.
- Who said no impact without explanation.

Produce a consensus matrix.

### Check 3: Interrupted or marginalized topics

Track every topic that was raised and cut off, deferred, or quietly abandoned.

For each interrupted topic, record:

- Who raised it.
- Who cut it off and how.
- Whether the topic was genuinely separate or hiddenly dependent.
- The risk of ignoring it.

### Check 4: Hidden dependencies

Find topics treated as separate even though one depends on the other.

For each dependency candidate, record:

- Topic A being decided now.
- Topic B deferred or dismissed.
- How A affects B.
- Risk of deciding A without B.

### Check 5: Scope drift

Compare the stated meeting goal with the actual decision path.

Track:

- What the meeting was supposed to decide.
- When the actual decision happened.
- Whether alternatives were genuinely explored or the first proposal won by default.

### Check 6: Severity mismatch

Check whether the group dismissed a low-frequency issue that may have high consequences.

For each finding, record:

- What was dismissed.
- On what basis.
- The actual consequence if it happens.
- The frequency multiplied by consequence.

### Check 7: Authority and social dynamics

Detect first-mover advantage, authority override, loudest-voice bias, and politeness traps.

## Workflow

### Step 1: Inventory

- List participants and roles.
- Build a topic timeline.
- List explicit and implicit decisions.

### Step 2: Run all checks

Apply each check independently.
One sentence can trigger multiple findings.

### Step 3: Cross-reference findings

Look for patterns across checks and note compounded risk.

### Step 4: Generate diagnostic questions

Questions must be specific, verifiable, and non-threatening.

Each finding should get 1 to 2 questions that can go to the next meeting.

### Step 5: Produce report

## Output format

```markdown
# Transcript Critique: [Meeting Name / Date]

## Meeting Metadata
- **Stated goal**: [what the meeting was supposed to decide]
- **Actual outcome**: [what was actually decided]
- **Participants**: [who was there, with roles]

## Critical Findings

### [Finding title]
**Checks triggered**: [which of the 7 checks]
**Severity**: Critical / High / Medium / Low
**Evidence**: "[exact quote from transcript]"
**Problem**: [what is wrong]
**Hidden risk**: [what could go wrong]
**Diagnostic question for next meeting**: "[specific question]"

## Consensus Audit

| Participant | Stated position | Genuine agreement? | Evidence |
|-------------|----------------|-------------------|----------|
| ... | ... | ... | ... |

## Deferred Topics — Dependency Check

| Topic deferred | Deferred by | Reason given | Hidden dependency with current decision? |
|----------------|-------------|-------------|----------------------------------------|
| ... | ... | ... | ... |

## Questions for Next Meeting

[Ordered list of diagnostic questions]
```

## Pitfalls

- Do not over-read silence.
- Do not treat every opinion as dangerous.
- Do not assume bad intent.
- Do not over-trust automated transcript ordering.
