# Aggregate Designer — Enforcement Rules

- ALWAYS verify that this is a concurrency / consistency-boundary problem first.
- DO NOT design an aggregate for simple CRUD.
- DO NOT design an aggregate for read-only flows.
- ALWAYS extract commands before boundary decisions.
- ALWAYS separate commands, facts/events, and queries.
- ALWAYS include a conflict matrix.
- ALWAYS ask about time-range conflicts when reservations or bookings appear.
- DO NOT skip the business-process sequencing probe.
- DO NOT propose implementation details unless requested.
- DO NOT continue when the fit check fails.
