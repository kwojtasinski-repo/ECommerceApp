# AC Pilot Prompt

> **Usage**: `#file:.github/prompts/ac-pilot.prompt.md` then append:
> `Run the Architect Consultant pilot on <case>.`

---

## Preconditions

- Stage 0 through Stage 4 (the first vertical slice) must be implemented and each stage's
  Definition of Done confirmed by `@stage-validator`.
- Pilot hypotheses and success criteria must already be recorded (per Blueprint §7/§12) —
  do not start a pilot without them written down first.

## Steps

1. Provide one real, naturally incomplete architectural case as input.
2. Run the slice: Intake → Classification → Classification Confirmation Gate → Stop.
3. Record the outcome using `.github/templates/PILOT_REPORT_TEMPLATE.md`.
4. Evaluate against the Blueprint §7/§12 criteria — do not judge by feature count.
5. Decide go/no-go per Blueprint §7 (content gaps → continue; structural gaps → STOP,
   raise to `@architecture-guardian` and the human).

## Do not

- Do not run the pilot without pre-recorded hypotheses and success criteria.
- Do not treat a "successful abstention" (system correctly refusing to guess) as failure.
