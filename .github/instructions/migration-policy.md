---
description: "Database migration policy for ECommerceApp Infrastructure" 
applyTo: "ECommerceApp.Infrastructure/Migrations/**"
---

# Database Migration Policy

Purpose
- Safe process for creating, reviewing, and applying database schema changes.

Rules
- Migration files MUST be created using `dotnet ef migrations add <Name>` from `ECommerceApp.Infrastructure` project directory.
- Migration files MUST include an explanatory comment in the migration class header describing intent and potential data impact.
- Do NOT edit existing migration files after they have been shared in main branches.

PR checklist for migrations
- Include SQL preview (`Script-Migration` or `dotnet ef migrations script`) showing changes.
- Provide a rollback plan (how to revert migration) in the PR description.
- Include integration tests that exercise the migration if schema changes affect runtime behavior.
- Ensure CI applies migrations against a test DB during integration tests.
- Tag `@team/architecture` and DB owners for approval.

Applying migrations to environments
- Dev: developers may run migrations locally.
- Staging: migrations applied via CI pipeline with a backup step.
- Production: migrations require ops approval, backup, and a maintenance window if needed.

Emergency fixes
- If an urgent schema fix is required, open an emergency PR and call out the risk and rollback steps explicitly.

