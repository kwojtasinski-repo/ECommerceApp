---
name: test-strategy-reviewer
description: >
  Reviews test strategy against the problem class of the code under test.
  Use when the user asks whether tests are at the right level, whether mocks are appropriate,
  or whether the test style matches the code's behavior.
argument-hint: "[path to test file or directory, or description of what to review]"
---

# Test Strategy Reviewer

This skill reviews whether the testing strategy matches the problem class of the code under test.
It does not review naming, formatting, fixture style, or coverage breadth unless those issues change the strategy.

## Hard rules

- MUST read both the test code and the production code it exercises.
- MUST classify each tested behavior before giving any recommendation.
- MUST ask the user to confirm the classification before continuing.
- MUST stop if the classification is ambiguous and the user has not resolved it.
- MUST keep the analysis scoped to testing strategy only.
- MUST not broaden into general test quality review.
- MUST not recommend a strategy before the problem class is confirmed.
- MUST split mixed code into separate behaviors when the file contains more than one problem class.

## Input handling

- If the user provides a path, read the test file(s) and the matching production code.
- If the user does not provide a path, ask what tests to review and stop.
- If more than one production target is plausible, ask the user which one to review and stop.

## Step 1: Classify the production code

Classify each behavior as one of these problem classes:

| Problem class | Signals |
|---|---|
| Transformation | Input goes in, output comes out, no durable state changes, no external side effects |
| Stateful Object | Identity matters, invariants are protected, state changes over time, prior state affects next outcome |
| Integration | The code coordinates multiple components, calls external systems, manages orchestration or transactions |

If a file mixes classes, classify each behavior separately instead of forcing one label.

### User confirmation gate

After forming the preliminary classification, present it to the user and wait for confirmation.

Use this structure:

> I read the code and tests. I classify [ClassName] as **[problem class]** because: [2-3 concrete signals]. Does that match your understanding?

Required answer options:

- Yes, it is [problem class]
- No, it is more like [other class], because...
- It is a mix: part [class A], part [class B]

Do not continue until the user answers.

## Step 2: Identify the current test strategy

For each test, determine the strategy actually used:

| Strategy | Signals |
|---|---|
| Output-based | The test calls the code and asserts on the returned value or final output |
| State-based | The test puts the object into a state, then verifies the resulting state through a getter, event, read model, or query |
| Interaction-based | The test uses mocks or stubs to verify calls, arguments, or call counts |

## Step 3: Compare against the recommended strategy

### Transformation

Recommended strategy: output-based.

Hard checks:

- Mocks or stubs on pure intermediate steps are usually unnecessary.
- Verifying internal method calls is an implementation leak.
- Testing private decomposition separately is over-testing unless the step is genuinely expensive or has side effects.

Before judging mocks on a transformation, ask one blocking question if needed:

> Are any of the mocked or stubbed steps financially expensive, performance-expensive, or side-effecting?

Treat the mock as legitimate only if the user confirms one of those exceptions.
If the step has side effects, treat the overall code as integration, not transformation.

### Stateful Object

Recommended strategy: output-based plus indirect state-based verification.

Hard checks:

- A stateful object should be tested after it has been put into a relevant prior state.
- Mocking internal parts of the object is a smell.
- If the test never verifies resulting state, event, or queryable outcome, it is missing the core contract.

Level-of-test gate:

- If the test is at aggregate level or facade/service level, ask which level is more appropriate before recommending a change.
- Ask about orchestration stability, orchestration complexity, and how the effect can be verified.
- Recommend facade/service-level tests only when the orchestration is simple and stable and the outcome is visible through a read model, view, or query.
- Keep aggregate-level tests when invariants are complex or the orchestration churn is high.

### Integration

Recommended strategy: interaction-based for unmanaged dependencies, real-instance state verification for managed dependencies.

Hard checks:

- If the code orchestrates other systems, do not pretend it is a pure transformation.
- Do not mock a managed database when a real database is available in the test scope.
- Mock unmanaged dependencies only at the system edge, not at an internal wrapper unless that wrapper is the actual contract boundary.
- Never assert interactions with stubs that only provide incoming data.

Dependency rule:

- Managed dependency: use a real instance and verify final state.
- Unmanaged dependency: mock the edge and verify the outgoing interaction.

If the integration code contains decision logic, separate the decision into a transformation and test that part output-based.
Test the orchestration shell interaction-based.

## Step 4: Report

For each test file or test class, report exactly this shape:

```markdown
### [TestClassName]

**Tests**: [ProductionClassName]
**Problem class**: [Transformation | Stateful Object | Integration | Mixed]
**Current strategy**: [output-based | state-based | interaction-based | mixed]
**Recommended strategy**: [what it should be]
**Verdict**: [OK | MISMATCH]

[If MISMATCH: explain the concrete change and why it improves the strategy.]
```

## Non-goals

- Do not comment on style, naming, or formatting unless they cause a strategy mismatch.
- Do not turn this into a full code review.
- Do not guess when the code class or the test intent is unclear.

## Principle order

1. Classify the production code.
2. Get user confirmation.
3. Identify the current test strategy.
4. Compare against the strategy that matches the problem class.
5. Report only the mismatch or confirmation result.