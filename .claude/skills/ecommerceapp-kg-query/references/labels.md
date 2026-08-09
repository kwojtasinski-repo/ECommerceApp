# ECommerceApp Knowledge Graph — Labels, Relationships, Modules

Mirrors `tools/kg/seed/ontology.cypher`. Use these exact identifiers in tool
calls.

## Node labels

| Label | Meaning |
|---|---|
| `System` | Top-level deployable (`ECommerceApp`) |
| `Host` | Deployment host — `ApiHost` (JSON REST) or `WebHost` (server-rendered MVC/Razor) |
| `Module` | Bounded-context container (14 total, see below) |
| `Entity` | EF Core entity (`IEntityTypeConfiguration<T>` under `Infrastructure/<Module>/Configurations/`) |
| `Repository` | `I*Repository` interface (`Domain/<Module>/`) + implementation |
| `Action` | Public method on an Application-layer `*Service` class |
| `Endpoint` | `[Http*]` method on an API controller (`API/Controllers/<Module>/`) |
| `Page` | MVC action + Razor view (`Web/Areas/<Area>/Controllers` or `Web/Controllers`) |
| `ScriptModule` | RequireJS/AMD client-side module under `wwwroot/js` (declaration marker: column-zero `define([...])`) |
| `Message` (+ `ModuleContract`) | Async fire-and-forget contract, delivered via Outbox/Inbox, 0..N handlers |
| `MessageHandler` | Handler for a `Message` (`IMessageHandler<T>` / `IIdAwareMessageHandler<T>`) |
| `Query` (+ `ModuleContract`) | Sync in-process contract via `ModuleClient`, exactly one handler, immediate response |
| `QueryHandler` | Handler for a `Query` (`IQueryHandler<TQuery,TResult>`) |
| `Job` | Background task (`IScheduledTask`) — trigger mode `Scheduled`, `Deferred`, or `Manual` |
| `Role` | Atomic `[Authorize(Roles=...)]` value (comma-joined aliases split into atomic nodes) |
| `Policy` | `[Authorize(Policy=...)]` value |

## The 14 modules (bounded contexts)

`AccountProfile`, `Backoffice`, `Catalog`, `IAM`, `Inventory`, `Checkout`,
`Orders`, `Payments`, `Coupons`, `Fulfillment`, `Communication`, `Currencies`,
`TimeManagement`, `Messaging`

`Catalog` merges Products + Images (one BC). `Inventory`'s folder is
`Availability`. `Checkout`'s folder is `Presale/Checkout`. `Backoffice` owns
no entities of its own — it's an intentional admin facade over every other
module (expected high fan-out via `OPERATES_ON`, not a modeling defect).

## Relationship vocabulary (10 verbs)

```
CONTAINS · PERSISTED_BY · OPERATES_ON · EXPOSED_BY · PUBLISHES ·
USES · GOVERNED_BY · DEPENDS_ON · HANDLED_BY · SCHEDULES
```

## Key triples

| From | Type | To | Notes |
|---|---|---|---|
| `Module` | `CONTAINS` | `Entity`, `Repository`, `Action`, `Endpoint`, `Page` | |
| `Entity` | `PERSISTED_BY` | `Repository` | |
| `Action` | `OPERATES_ON` | `Entity` | |
| `Action` | `EXPOSED_BY` | `Endpoint`, `Page` | |
| `Action` | `PUBLISHES` | `Message` | |
| `Action` | `USES` | `Query` | |
| `{Action, MessageHandler}` | `SCHEDULES` | `Job` | e.g. `OrderPlacedHandler → PaymentWindowExpiredJob` |
| `Message` | `HANDLED_BY` | `MessageHandler` | 0..N handlers, eventual consistency |
| `Query` | `HANDLED_BY` | `QueryHandler` | exactly one handler, immediate response |
| `{Endpoint, Page}` | `GOVERNED_BY` | `Role`, `Policy` | never from `Action` directly |
| `Job` | `OPERATES_ON` | `Entity` | |
| `Job` | `PUBLISHES` | `Message` | |
| `Page` | `USES` | `ScriptModule` | |

No `Module -[:DEPENDS_ON]-> Module` edge is stored — that fact is always
derived live through `GetModuleDependencies`, not materialized, so it can't
drift from the contracts it summarizes.

## Two channels, one shared verb

`Message` (async, Outbox/Inbox, 0..N handlers) and `Query` (sync,
`ModuleClient`, exactly one handler) are deliberately distinguished by node
label but share the `HANDLED_BY` verb — opposite delivery guarantees, same
relationship name.

## Job trigger modes

- `Deferred` — per-entity, one-shot, enqueued via `IDeferredJobScheduler.ScheduleAsync`. The only mode with a statically findable `SCHEDULES` edge.
- `Scheduled` — recurring; cron string is runtime DB data, not in the graph.
- `Manual` — any registered Job is Administrator-triggerable; not a per-job edge.

Most `Job` nodes carry `triggerMode: null` — that means `Scheduled` or
`Manual`, not a modeling gap.

## Not modeled (by design)

- Module-to-module coupling as a stored edge (derived live instead, see above).
- `Feature`/event-storming nodes — that information lives in event-storming docs, not code.
- Query/command classification on `Action` — .NET has no direct equivalent of a readonly-transaction marker.
