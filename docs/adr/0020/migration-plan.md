## Migration plan

Migration is incremental — no big-bang refactor:

1. Create `Application/Backoffice/` folder structure and `Extensions.cs`
2. For each BC atomic switch, move admin-specific query logic from legacy service/controller into the corresponding Backoffice service
3. After ADR-0013 is fully implemented, audit Backoffice services to confirm no direct DbContext access
4. Remove legacy admin query methods from BC services once all Backoffice services are migrated
