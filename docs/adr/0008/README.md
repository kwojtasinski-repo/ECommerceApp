# ADR-0008: Supporting/Currencies BC

**Status**: Accepted
**BC**: Supporting/Currencies

## What this decision covers
Design of currency rate synchronization via the NBP (National Bank of Poland) API,
`ICurrencyService`, `CurrencyRateSyncTask`, and the currencies DB schema.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0008-supporting-currencies-bc-design.md | Full design: ICurrencyService, NBP API adapter, CurrencyRateSyncTask, schema | Understanding currency sync |
| example-implementation/nbp-api-integration.md | NBP API call flow and rate sync schedule | Working with currency rates |

## Key rules
- Currency rates are read-only in most BCs — only Currencies BC writes them
- `CurrencyRateSyncTask` is an `IScheduledTask` owned by TimeManagement BC
- Switch complete — legacy CurrencyController removed

## Related ADRs
- ADR-0009 (TimeManagement) — CurrencyRateSyncTask scheduling
