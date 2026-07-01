# Architect Consultant — Framework Documentation

This folder contains the operating documents for the **Architect Consultant** framework:
an orchestration layer that coordinates architectural decision-support work using a
frozen pipeline, a metadata-driven knowledge library, and evidence-grounded confidence.

These documents describe the framework's **own** implementation, not ECommerceApp's
business bounded contexts. See `docs/architecture/bounded-context-map.md` for that.

## Documents

- [`Implementation-Blueprint-v1.md`](Implementation-Blueprint-v1.md) — what to build,
  in what order, stage by stage. Frozen.
- [`IMPLEMENTATION_RULES.md`](IMPLEMENTATION_RULES.md) — mandatory implementation
  directives, written for models of any capability level. Frozen.
- [`IMPLEMENTATION_PLAYBOOK.md`](IMPLEMENTATION_PLAYBOOK.md) — how to use the above
  documents together in practice, including LLM role assignment and blocker/pilot
  workflows.
- [`IMPLEMENTATION_STATE.md`](IMPLEMENTATION_STATE.md) — current stage, status, and
  active blockers. Updated after every task — not frozen, always current.

## Related tooling

- `.github/agents/coordinator.md` and the other `.github/agents/*` files listed in the
  Playbook's "Working with Different LLMs" section — the orchestration and execution
  agents that implement this framework.
- `.github/prompts/ac-*.prompt.md` — thin trigger prompts for common tasks.
- `.github/templates/TASK_TEMPLATE.md`, `BLOCKER_TEMPLATE.md`, `REVIEW_TEMPLATE.md`,
  `PILOT_REPORT_TEMPLATE.md`, `STAGE_REPORT_TEMPLATE.md` — canonical operational
  templates referenced by the agents above.

## Reading order

1. `IMPLEMENTATION_STATE.md` — where are we right now?
2. `Implementation-Blueprint-v1.md` — what does the current stage require?
3. `IMPLEMENTATION_RULES.md` — what constraints apply while doing it?
4. `IMPLEMENTATION_PLAYBOOK.md` — how do I actually run this with a team of humans and LLMs?
