# ADR-0005: AccountProfile BC — UserProfile Aggregate Design

**Status**: Accepted
**BC**: AccountProfile

## What this decision covers
`UserProfile` aggregate, `Address`, `ContactDetail` entities, and the ProfileController migration.
Switch is complete.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0005-accountprofile-bc-userprofile-aggregate-design.md | Full design: UserProfile aggregate, address/contact sub-entities | Working with user profile features |

## Key rules
- Switch complete — legacy CustomerController/AddressController/ContactDetailController removed
- `string UserId` only — no `ApplicationUser` navigation property

## Related ADRs
- ADR-0019 (IAM) — UserId originates from IAM BC
