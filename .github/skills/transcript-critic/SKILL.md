---
name: transcript-critic
description: Critique meeting transcripts for hidden decisions, false consensus, marginalized voices, scope drift, and unsupported claims. Use only on explicit request.
argument-hint: "[meeting transcript or discussion notes]"
---

# Transcript Critic

## Enforcement Rules

- ALWAYS analyze only when the user explicitly asks for critique, review, or diagnosis.
- NEVER summarize the transcript.
- NEVER infer intent, tone, or motive without evidence.
- ALWAYS quote the exact transcript line that triggered each finding.
- ALWAYS distinguish fact, opinion, hearsay, and declarative conclusion.
- NEVER turn silence into a finding unless there is a concrete decision risk.
- ALWAYS produce diagnostic questions that can be taken to the next meeting.

Analyze meeting transcripts to surface decision-making problems that a naive summary would miss: false consensus, marginalized voices, opinions disguised as facts, hidden dependencies between "separate" topics, and scope drift.

**Output goal**: a structured critique with severity, evidence, and diagnostic questions. This is not a summary.

## When to Use

- After a meeting where decisions were made
- Before acting on meeting notes
- When preparing questions for a follow-up meeting
- When reviewing notes for missing decision logic

## When NOT to Use

- The user wants a summary
- The user wants a rewrite of the transcript
- The user wants a decision made instead of a critique

---

## Analysis Framework

Run all seven checks on the transcript. Each check produces findings independently.

### Check 1: Fact vs Opinion vs Hearsay

Classify every claim:

- **Fact** — verifiable with evidence in the transcript
- **Opinion** — stated without evidence
- **Hearsay** — information from a third party
- **Declarative conclusion** — stated as fact without support

Track when opinion or hearsay gets treated as fact later.

### Check 2: Consensus Audit

Verify who explicitly agreed, who was only compliant, and who was never asked.

### Check 3: Interrupted or Marginalized Topics

Track topics that were raised and cut off, deferred, or quietly abandoned.

### Check 4: Hidden Dependencies

Find topics treated as separate even though one depends on the other.

### Check 5: Scope Drift

Compare the stated meeting goal with the actual decision path.

### Check 6: Severity Mismatch

Check whether the group dismissed a low-frequency issue that may have high consequences.

### Check 7: Authority and Social Dynamics

Detect first-mover advantage, authority override, loudest-voice bias, and politeness traps.

---

## Workflow

### Step 1: Inventory

- List participants and roles
- Build a topic timeline
- List explicit and implicit decisions

### Step 2: Run All Checks

Apply each check independently. One sentence can trigger multiple findings.

### Step 3: Cross-Reference Findings

Look for patterns across checks and note compounded risk.

### Step 4: Generate Diagnostic Questions

Questions must be specific, verifiable, and non-threatening.

### Step 5: Produce Report

---

## Output Format

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

---

## Pitfalls

- Do not over-read silence.
- Do not treat every opinion as dangerous.
- Do not assume bad intent.
- Do not over-trust automated transcript ordering.
