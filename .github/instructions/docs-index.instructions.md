---
applyTo: "**"
---

# Docs Index — Lookup Table for Copilot

> This file teaches Copilot **what documentation exists** and **when to read it**.
> The docs themselves are human-owned — never modify them. Just look up and read.

## ADRs (`docs/adr/`)

Read an ADR **only** when the "When to read" condition matches the files you are editing.

| ADR  | Title                                                 | When to read                                       |
| ---- | ----------------------------------------------------- | -------------------------------------------------- |
| 0001 | Project overview and technology stack                 | First-time context; project-wide tech decisions    |
| 0002 | Post-event-storming architectural evolution strategy  | Any BC migration or parallel-change work           |
| 0003 | Feature folder organization for new BC code           | Creating new folders/namespaces for a BC           |
| 0004 | Module taxonomy and bounded context grouping          | BC grouping, module naming, namespace decisions    |
| 0005 | AccountProfile BC — UserProfile aggregate design      | Editing AccountProfile domain or services          |
| 0006 | TypedId and value objects as shared domain primitives | Editing `Domain/Shared/**` (TypedId, Money, Price) |
| 0007 | Catalog BC — Product, Category, Tag aggregate design  | Editing Catalog domain or services                 |
| 0008 | Supporting/Currencies BC design                       | Editing Currency, CurrencyRate, NBP integration    |
| 0009 | Supporting/TimeManagement BC design                   | Editing time-related domain logic                  |
| 0010 | In-memory message broker for cross-BC communication   | Adding domain events or cross-BC messaging         |
| 0011 | Inventory/Availability BC design                      | Editing Inventory domain or stock logic            |
| 0012 | Presale/Checkout BC design                            | Editing checkout flow, cart logic                  |
| 0013 | Per-BC DbContext interfaces                           | Adding new DbContext or per-BC data access         |
| 0014 | Sales/Orders BC design                                | Editing Order, OrderItem domain or services        |
| 0015 | Sales/Payments BC design                              | Editing Payment, PaymentState domain or services   |
| 0016 | Sales/Coupons BC design                               | Editing Coupon, CouponType, CouponUsed             |
| 0017 | Sales/Fulfillment BC design                           | Editing fulfillment or shipping logic              |
| 0018 | Supporting/Communication BC design                    | Editing notification or messaging features         |
| 0019 | Identity/IAM BC design                                | Editing Identity, roles, authentication            |
| 0020 | Backoffice BC design                                  | Editing admin/backoffice features                  |
| 0021 | Frontend error pipeline and JS migration strategy     | Editing `wwwroot/js/**`, error handling in Views   |

## Architecture docs (`docs/architecture/`)

| File                     | When to read                                                                                        |
| ------------------------ | --------------------------------------------------------------------------------------------------- |
| `bounded-context-map.md` | Before adding cross-BC dependencies, proposing new aggregates, or checking BC implementation status |

## Pattern templates (`docs/patterns/`)

| File                         | When to read                                                                                                          |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| `implementation-patterns.md` | When implementing a new aggregate, value object, repository, facade, or handler — contains 14 reusable code templates |

## Roadmaps (`docs/roadmap/`)

| File                        | When to read                                                             |
| --------------------------- | ------------------------------------------------------------------------ |
| `README.md`                 | Before any BC implementation — shows dependency order and phase overview |
| `orders-atomic-switch.md`   | Working on Sales/Orders BC (highest-priority unblocking item)            |
| `payments-atomic-switch.md` | Working on Sales/Payments BC (blocked by Orders)                         |
| `iam-atomic-switch.md`      | Working on Identity/IAM BC (coordinate with Orders switch)               |
| `presale-slice2.md`         | Working on Presale/Checkout Slice 2 (blocked by Orders)                  |
| `frontend-pipeline.md`      | Working on frontend JS/error pipeline (ADR-0021 phases)                  |

## Context files (`.github/context/`)

| File               | When to read                                                       |
| ------------------ | ------------------------------------------------------------------ |
| `project-state.md` | **Always** before editing BC-related code — check if BC is blocked |
| `known-issues.md`  | **Always** before fixing any bug — check if already tracked        |
