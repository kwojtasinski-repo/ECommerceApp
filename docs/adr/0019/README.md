# ADR-0019: Identity/IAM BC

**Status**: Accepted
**BC**: Identity/IAM

## What this decision covers
`ApplicationUser` deletion, `IamDbContext` extending `DbContext` (not `IdentityDbContext`),
JWT + Refresh Token design, and the IAM switch.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0019-identity-iam-bc-design.md | Full design: IAM aggregate, refresh token, AuthController, switch details | Working with authentication or IAM |

## Key rules
- Switch complete — `Domain.Model.ApplicationUser` deleted, `Context` changed to `DbContext`
- Refresh tokens: `POST /api/auth/refresh` + `POST /api/auth/revoke`
- JWT claims include `api:purchase` for trusted API users (see ADR-0025)

## Related ADRs
- ADR-0025 (API tiered access) — TrustedApiUser = IAM claims
