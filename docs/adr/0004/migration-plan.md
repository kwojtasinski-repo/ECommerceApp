## Migration plan

Incremental — per ADR-0003 Strangler Fig policy:
1. All new BCs created directly under their canonical group folder.
2. Existing BCs migrated one group at a time when a dedicated refactoring ADR is accepted.
3. `Supporting` group (Currencies) is the lowest-risk migration candidate as a reference run.
