# Flow Analysis Prompt

> **Usage**: Reference this file in Copilot Chat with `#file:.github/prompts/flow-analysis.prompt.md`
> then append the flow or feature to analyze.
>
> Examples:
> - `#file:.github/prompts/flow-analysis.prompt.md Analyze the presale checkout flow.`
> - `#file:.github/prompts/flow-analysis.prompt.md Analyze the order placement flow.`

---

## Purpose

Trace a user-facing flow in **both directions** to catch gaps that only appear when walking
the path backwards. Forward analysis finds missing steps; reverse analysis finds dead ends,
wrong redirects, and states the user can get stuck in.

---

## Step 1 — Identify the flow boundaries

State the two endpoints of the flow:

| | |
|---|---|
| **Start** | First user action (e.g. "adds item to cart") |
| **End** | Final system state (e.g. "order placed, payment record exists") |

List every actor involved: user, controller actions, services, domain events, background jobs.

---

## Step 2 — Forward trace (Start → End, happy path)

Walk every step in sequence. For each step record:

| Step | User action / trigger | Controller / service called | State change | Next step |
|---|---|---|---|---|
| 1 | | | | |
| N | | | | |

**At each step ask:**
- What precondition must be true for this step to proceed?
- What does the system write / change?
- What is returned / rendered to the user?

---

## Step 3 — Forward trace (Start → End, failure branches)

For each step in Step 2, list every result that is **not** the happy path:

| Step | Failure case | System response | User lands on | Correct? |
|---|---|---|---|---|
| | | | | ✅ / ❌ / ❓ |

---

## Step 4 — Reverse trace (End → Start)

Start from the final state and walk backwards. For each step ask:

- **How did the system get into this state?** Which prior step produced it?
- **What if that prior step never ran?** Is there a guard? What does the user see?
- **Can the user re-enter this step from the UI?** If yes, what happens?

| Step (reversed) | Expected predecessor | Guard present? | What breaks if predecessor is missing? |
|---|---|---|---|
| N | | yes / no | |
| 1 | | yes / no | |

---

## Step 5 — Edge cases surface

List every anomaly found during Steps 2–4:

| # | Description | Direction found | Severity | Fix needed? |
|---|---|---|---|---|
| 1 | | forward / reverse | low / medium / high | yes / no |

**Common things to check:**
- Timer or TTL that can expire between two steps (e.g. reservation window)
- Background job that races with a synchronous redirect
- Missing redirect / 404 when a precondition fails
- UI state that doesn't match DB state (e.g. banner shows but reservation already deleted)
- Re-entrant flows (user hits "back" or refreshes mid-flow)
- Acceptance windows — requests submitted just after a deadline

---

## Step 6 — Verdict

| Category | Result |
|---|---|
| Happy path complete? | ✅ / ❌ |
| All failure branches handled? | ✅ / ❌ / partial |
| Reverse path consistent? | ✅ / ❌ / partial |
| Edge cases requiring fixes | list or "none" |
| Recommended next actions | |

---

## Notes for this project

- Check `CheckoutResult` switch arms in every controller action — an unhandled arm silently falls to the `_` default.
- Verify that background jobs (e.g. `SoftReservationExpiredJob`, `PaymentWindowExpiredJob`) do not create race conditions with synchronous redirects — `MessagingOptions.UseBackgroundDispatcher` is `false` in Web but may differ in other hosts.
- For any TTL-bounded flow, verify the acceptance window (`PlaceOrderAcceptanceWindow`) is shorter than the grace period (`SoftReservationGracePeriod`) — enforced by `PresaleOptionsValidator`.
- After a reverse trace, if an edge case is found, check `known-issues.md` before opening a new one.
