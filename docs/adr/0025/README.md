# ADR-0025: API Tiered Access — Trusted Purchase Policy

**Status**: Accepted
**BC**: API

## What this decision covers
`TrustedApiUser` policy (authenticated + `api:purchase` claim OR Service/Manager/Administrator role),
`MaxApiQuantityFilter` (max 5 units/line), and `WebOptions:BaseUrl` payment URL.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0025-api-tiered-access-trusted-purchase-policy.md | Full policy: TrustedApiUser definition, quantity caps, payment URL | Working with API purchase endpoints |

## Key rules
- API max: 5 units/line (`MaxApiQuantityFilter`) — Web max: 99 (`AddToCartDtoValidator`)
- `TrustedApiUser` = `api:purchase` claim OR `Service`/`Manager`/`Administrator` role
- Never cap `Shared.Quantity` value object — caps are at request/filter level only

## Related ADRs
- ADR-0019 (IAM) — api:purchase claim issued during auth
