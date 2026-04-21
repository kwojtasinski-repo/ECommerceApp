<#
.SYNOPSIS
    Restructures docs/adr/ flat files into per-ADR numbered folders.

.DESCRIPTION
    Tier 1 ADRs:
      - Create docs/adr/{XXXX}/ folder
      - Move original file into folder (keeps original filename)
      - Extract amendment sections  → amendments/
      - Extract conformance checklist → checklist.md
      - Extract migration plan       → migration-plan.md
      - Strip Implementation Status from main file (already in project-state.md)
      - Create README.md (local router for humans + Copilot)
      - Create example-implementation/ folder stubs

    Tier 2 ADRs:
      - Create docs/adr/{XXXX}/ folder
      - Move original file into folder (keeps original filename)
      - Strip Implementation Status from main file only
      - Create README.md
      - Create example-implementation/ folder stubs

    Tier 3 ADRs:
      - Create docs/adr/{XXXX}/ folder
      - Move original file into folder (keeps original filename)
      - Strip Implementation Status from main file only
      - Create README.md

    Script is idempotent — safe to re-run after a partial or complete migration.
    No .csproj or .sln changes — VS reload NOT triggered.

.EXAMPLE
    cd C:\Projekty\DotNet\ECommerceApp
    .\tools\restructure-adrs.ps1
#>

param([string]$RepoRoot = "C:\Projekty\DotNet\ECommerceApp")

$adrBase = Join-Path $RepoRoot "docs\adr"
$ErrorActionPreference = "Stop"

# ── Colour output ───────────────────────────────────────────────────────────
function Info  { param($m) Write-Host "    $m"      -ForegroundColor Cyan    }
function OK    { param($m) Write-Host "  + $m"      -ForegroundColor Green   }
function Warn  { param($m) Write-Host "  ! $m"      -ForegroundColor Yellow  }
function Step  { param($m) Write-Host "`n>> $m"     -ForegroundColor Magenta }
function Done  { param($m) Write-Host "`n== $m =="  -ForegroundColor White   }

# ── File helpers ────────────────────────────────────────────────────────────
function Ensure-Dir([string]$path) {
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }
}

function Write-UTF8([string]$path, [string]$content) {
    Ensure-Dir (Split-Path $path -Parent)
    [System.IO.File]::WriteAllText(
        $path,
        $content.TrimEnd() + "`n",
        [System.Text.Encoding]::UTF8
    )
    OK $path.Replace($RepoRoot, "").TrimStart("\")
}

# ── Markdown section helpers ─────────────────────────────────────────────────
# Extract a section from lines[] matching headingRegex, up to next heading of same/higher level
function Get-Section {
    param([string[]]$lines, [string]$headingRegex)

    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $headingRegex) { $start = $i; break }
    }
    if ($start -eq -1) { return $null }

    $lvl = ([regex]::Match($lines[$start], '^(#{1,6})\s').Groups[1].Value).Length
    $end  = $lines.Count
    for ($i = $start + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^#{1,$lvl} ") { $end = $i; break }
    }
    return ($lines[$start..($end - 1)] -join "`n").Trim()
}

# Remove one or more sections from lines[], return cleaned array
function Remove-Sections {
    param([string[]]$lines, [string[]]$headingRegexes)

    $out = [System.Collections.Generic.List[string]]::new()
    $i = 0
    while ($i -lt $lines.Count) {
        $matched = $false
        foreach ($rx in $headingRegexes) {
            if ($lines[$i] -match $rx) {
                $lvl = ([regex]::Match($lines[$i], '^(#{1,6})\s').Groups[1].Value).Length
                $i++
                while ($i -lt $lines.Count -and $lines[$i] -notmatch "^#{1,$lvl} ") { $i++ }
                $matched = $true; break
            }
        }
        if (-not $matched) { $out.Add($lines[$i]); $i++ }
    }
    return $out.ToArray()
}

# Remove trailing blank lines from a string array
function Trim-Lines([string[]]$lines) {
    $end = $lines.Count - 1
    while ($end -ge 0 -and [string]::IsNullOrWhiteSpace($lines[$end])) { $end-- }
    if ($end -lt 0) { return @() }
    return $lines[0..$end]
}

function Create-ExampleStubs {
    param([string]$folderPath, [string[]]$exampleFiles)

    if ($null -eq $exampleFiles -or $exampleFiles.Count -eq 0) {
        return
    }

    $exDir = Join-Path $folderPath "example-implementation"
    foreach ($ef in $exampleFiles) {
        $efPath = Join-Path $exDir $ef
        if (-not (Test-Path $efPath)) {
            $title = [System.IO.Path]::GetFileNameWithoutExtension($ef) -replace '-', ' '
            $stub  = "# $title`n`n> Stub: extract relevant content from the main ADR or amendments into this file.`n"
            Write-UTF8 $efPath $stub
        }
    }
}

# ── Process a single ADR ─────────────────────────────────────────────────────
function Process-Adr {
    param(
        [string]   $srcFile,           # full path to flat .md file
        [string]   $folderPath,        # destination folder
        [string[]] $amendmentRegexes,  # H2 heading regexes to extract as amendments
        [string[]] $amendmentNames,    # output filenames  (parallel to above)
        [bool]     $hasMigrationPlan,  # extract ## Migration plan?
        [bool]     $hasChecklist,      # extract ## Conformance checklist?
        [string]   $readmeContent,     # full README.md content
        [string[]] $exampleFiles       # placeholder filenames under example-implementation/
    )

    $name  = Split-Path $srcFile -Leaf
    $dest  = Join-Path $folderPath $name
    $mainFile = $null

    Ensure-Dir $folderPath

    # Copy original into folder if not already there. On reruns, operate on the
    # file already inside the destination folder.
    if (Test-Path $srcFile) {
        if ($srcFile -ne $dest) {
            Copy-Item $srcFile $dest -Force
            $mainFile = $dest
        } else {
            $mainFile = $srcFile
        }
    } elseif (Test-Path $dest) {
        $mainFile = $dest
    } else {
        Warn "Source file missing: $($srcFile.Replace($RepoRoot, '').TrimStart('\'))"
        return
    }

    $lines = [System.IO.File]::ReadAllLines($mainFile, [System.Text.Encoding]::UTF8)

    # ── Extract amendments ──────────────────────────────────────────────────
    $amendDir = Join-Path $folderPath "amendments"
    for ($a = 0; $a -lt $amendmentRegexes.Count; $a++) {
        $section = Get-Section $lines $amendmentRegexes[$a]
        if ($section) {
            Ensure-Dir $amendDir
            Write-UTF8 (Join-Path $amendDir $amendmentNames[$a]) $section
        } else {
            Warn "Amendment not found: $($amendmentRegexes[$a]) in $name"
        }
    }

    # ── Extract checklist ───────────────────────────────────────────────────
    if ($hasChecklist) {
        $section = Get-Section $lines '^## Conformance checklist'
        if ($section) {
            Write-UTF8 (Join-Path $folderPath "checklist.md") $section
        }
    }

    # ── Extract migration plan ──────────────────────────────────────────────
    if ($hasMigrationPlan) {
        $section = Get-Section $lines '^## Migration plan'
        if ($section) {
            Write-UTF8 (Join-Path $folderPath "migration-plan.md") $section
        }
    }

    # ── Strip extracted sections + Implementation Status from main file ─────
    $stripRegexes = @('^## Implementation Status')
    if ($hasChecklist)    { $stripRegexes += '^## Conformance checklist' }
    if ($hasMigrationPlan){ $stripRegexes += '^## Migration plan' }
    foreach ($rx in $amendmentRegexes) { $stripRegexes += $rx }

    $cleaned = Remove-Sections $lines $stripRegexes
    $cleaned = Trim-Lines $cleaned
    [System.IO.File]::WriteAllLines($mainFile, $cleaned, [System.Text.Encoding]::UTF8)
    OK "Cleaned main: $($mainFile.Replace($RepoRoot,'').TrimStart('\'))"

    # ── Write README ────────────────────────────────────────────────────────
    Write-UTF8 (Join-Path $folderPath "README.md") $readmeContent

    # ── Create example-implementation stubs ─────────────────────────────────
    Create-ExampleStubs $folderPath $exampleFiles

    # ── Remove original flat file ────────────────────────────────────────────
    if ($srcFile -ne $dest -and (Test-Path $srcFile)) {
        Remove-Item $srcFile -Force
        OK "Removed flat: $($srcFile.Replace($RepoRoot,'').TrimStart('\'))"
    }
}

# ── Simple folder-only move (Tier 2/3 — no section extraction) ──────────────
function Move-Simple {
    param([string]$srcFile, [string]$folderPath, [string]$readmeContent)

    $name = Split-Path $srcFile -Leaf
    $dest = Join-Path $folderPath $name
    $mainFile = $null

    Ensure-Dir $folderPath

    if (Test-Path $srcFile) {
        if ($srcFile -ne $dest) {
            Copy-Item $srcFile $dest -Force
            Remove-Item $srcFile -Force
            OK "Moved: $($dest.Replace($RepoRoot,'').TrimStart('\'))"
            $mainFile = $dest
        } else {
            $mainFile = $srcFile
        }
    } elseif (Test-Path $dest) {
        $mainFile = $dest
    } else {
        Warn "Source file missing: $($srcFile.Replace($RepoRoot, '').TrimStart('\'))"
        return
    }

    # Strip Implementation Status from any tier
    $lines   = [System.IO.File]::ReadAllLines($mainFile, [System.Text.Encoding]::UTF8)
    $cleaned = Remove-Sections $lines @('^## Implementation Status')
    $cleaned = Trim-Lines $cleaned
    [System.IO.File]::WriteAllLines($mainFile, $cleaned, [System.Text.Encoding]::UTF8)

    Write-UTF8 (Join-Path $folderPath "README.md") $readmeContent
}

# ════════════════════════════════════════════════════════════════════════════
# TIER 1 — Full restructure
# ════════════════════════════════════════════════════════════════════════════

Step "ADR-0009 Supporting/TimeManagement"
Process-Adr `
  -srcFile         "$adrBase\0009-supporting-timemanagement-bc-design.md" `
  -folderPath      "$adrBase\0009" `
  -amendmentRegexes @(
      '^## Amendment — Design Revisions'
  ) `
  -amendmentNames  @(
      'a1-design-revisions.md'
  ) `
  -hasMigrationPlan $true `
  -hasChecklist     $true `
  -exampleFiles    @(
      'ischeduledtask-implementation.md',
      'ideferred-job-scheduler-usage.md',
      'job-registration-di.md'
  ) `
  -readmeContent   @'
# ADR-0009: Supporting/TimeManagement BC

**Status**: Accepted — Amended 2026-02-26
**BC**: Supporting/TimeManagement
**Last amended**: 2026-02-26

## What this decision covers
Design of the job scheduling infrastructure: `IScheduledTask` (recurring), `IDeferredJobScheduler`
(domain-triggered), deferred queue with exponential backoff, and zombie detection.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0009-supporting-timemanagement-bc-design.md | Core design: job taxonomy, IScheduledTask, IDeferredJobScheduler, DB schema | Understanding the job engine |
| amendments/a1-design-revisions.md | A1–A5: DB-first config, table split, status simplification, retry backoff, tick alignment | Working with job scheduling internals |
| checklist.md | Implementation conformance rules | Code review of job implementations |
| migration-plan.md | Implementation steps (completed) | Historical reference |
| example-implementation/ischeduledtask-implementation.md | How to implement a new scheduled task | Adding a new recurring job |
| example-implementation/ideferred-job-scheduler-usage.md | How to schedule a deferred job from a domain event | Wiring domain events to jobs |
| example-implementation/job-registration-di.md | DI registration pattern for jobs | Setting up AddXxxServices() |

## Key rules
- All job definitions live in TimeManagement BC — never define job metadata in the calling BC
- Amendments A1–A5 override the original design in §4 and §6 — read amendments first
- `IDeferredJobScheduler` is for one-time domain-triggered jobs; `IScheduledTask` is for recurring

## Related ADRs
- ADR-0002 (Parallel Change strategy) — job migration follows parallel change
- ADR-0011 (Inventory) — uses `StockAdjustmentJob` via `IDeferredJobScheduler`
- ADR-0015 (Payments) — uses `PaymentWindowExpiredJob`
'@

Step "ADR-0010 In-Memory Message Broker"
Process-Adr `
  -srcFile         "$adrBase\0010-in-memory-message-broker-for-cross-bc-communication.md" `
  -folderPath      "$adrBase\0010" `
  -amendmentRegexes @(
      '^## Amendment — Retry, Observability'
  ) `
  -amendmentNames  @(
      'a1-retry-observability-configuration.md'
  ) `
  -hasMigrationPlan $true `
  -hasChecklist     $true `
  -exampleFiles    @(
      'publish-message-example.md',
      'register-handler-example.md',
      'multi-handler-pattern.md'
  ) `
  -readmeContent   @'
# ADR-0010: In-Memory Message Broker for Cross-BC Communication

**Status**: Accepted — Amended 2026-02-26
**BC**: Shared infrastructure
**Last amended**: 2026-02-26

## What this decision covers
Design of `IMessageBroker` / `ModuleClient` for in-process cross-BC messaging.
Every BC uses this for publishing and subscribing to integration messages.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0010-in-memory-message-broker-for-cross-bc-communication.md | Core design: IMessageBroker, IMessageHandler<T>, ModuleClient, DI setup | Understanding cross-BC communication |
| amendments/a1-retry-observability-configuration.md | Retry policy, structured logging, configuration overrides | Debugging message delivery or tuning retries |
| checklist.md | Handler implementation rules | Code review of new handlers |
| migration-plan.md | Implementation steps (completed) | Historical reference |
| example-implementation/publish-message-example.md | How to publish an integration message from a service | Writing a publisher |
| example-implementation/register-handler-example.md | How to implement IMessageHandler<T> and register it | Writing a subscriber |
| example-implementation/multi-handler-pattern.md | Multiple handlers for one message type | Fan-out scenarios |

## Key rules
- All cross-BC communication goes through `IMessageBroker` — never inject a foreign BC service directly
- Handlers must be idempotent — broker delivers at-least-once
- Amendment A1 adds retry + observability config — use `MessageBrokerOptions` to tune

## Related ADRs
- ADR-0002 (architecture strategy) — BC boundary enforcement
- ADR-0026 (Saga) — compensation flow uses message broker fan-out
- Every BC ADR (0011–0018) — all use this for cross-BC events
'@

Step "ADR-0011 Inventory/Availability"
Process-Adr `
  -srcFile         "$adrBase\0011-inventory-availability-bc-design.md" `
  -folderPath      "$adrBase\0011" `
  -amendmentRegexes @(
      '^## Design Amendment — Fulfillment Message Consumption'
  ) `
  -amendmentNames  @(
      'a1-fulfillment-message-consumption.md'
  ) `
  -hasMigrationPlan $true `
  -hasChecklist     $true `
  -exampleFiles    @(
      'stock-adjustment-algorithm.md',
      'two-phase-reservation-flow.md',
      'cross-bc-message-wiring.md'
  ) `
  -readmeContent   @'
# ADR-0011: Inventory/Availability BC — StockItem Aggregate Design

**Status**: Accepted — Amended
**BC**: Inventory/Availability
**Last amended**: 2025-06-27

## What this decision covers
Design of `StockItem` counter aggregate, `Reservation` entity, two-phase reservation,
deferred `StockAdjustmentJob` with coalescing, and cross-BC message subscriptions.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0011-inventory-availability-bc-design.md | Core design: StockItem, Reservation, two-phase reserve, §8a summary, cross-BC wiring | Understanding Inventory BC structure |
| amendments/a1-fulfillment-message-consumption.md | Adds RefundApproved + ShipmentDispatched handler wiring | Working with Fulfillment→Inventory integration |
| checklist.md | Conformance rules for StockItem and Reservation | Code review |
| migration-plan.md | Implementation steps (completed) | Historical reference |
| example-implementation/stock-adjustment-algorithm.md | Full §8a: deferred write, command coalescing, version-match delete, Flow A + Flow B | Implementing or debugging StockAdjustmentJob |
| example-implementation/two-phase-reservation-flow.md | Reserve → Confirm → Fulfill lifecycle with timeout | Working with reservation state machine |
| example-implementation/cross-bc-message-wiring.md | All inbound/outbound message handlers and their actions | Adding a new message subscription |

## Key rules
- `StockItem` never loads `Reservation` as a collection — always query separately
- `Adjust` is always deferred through `StockAdjustmentJob` — never inline
- Amendment A1 adds two new inbound handlers — check it before modifying message subscriptions

## Related ADRs
- ADR-0010 (message broker) — all cross-BC triggers
- ADR-0009 (TimeManagement) — `StockAdjustmentJob` uses `IDeferredJobScheduler`
- ADR-0017 (Fulfillment) — publisher of `RefundApproved`
'@

Step "ADR-0012 Presale/Checkout"
Process-Adr `
  -srcFile         "$adrBase\0012-presale-checkout-bc-design.md" `
  -folderPath      "$adrBase\0012" `
  -amendmentRegexes @() `
  -amendmentNames  @() `
  -hasMigrationPlan $true `
  -hasChecklist     $true `
  -exampleFiles    @(
      'checkout-confirm-flow.md',
      'soft-reservation-lifecycle.md',
      'price-change-detection.md'
  ) `
  -readmeContent   @'
# ADR-0012: Presale/Checkout BC

**Status**: Accepted
**BC**: Presale/Checkout
**Last amended**: —

## What this decision covers
Design of the shopping cart, soft reservations, price-change detection, and
`CheckoutService.ConfirmAsync` which places an order via the Orders BC ACL.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0012-presale-checkout-bc-design.md | Core design: CartLine, SoftReservation, ICheckoutService, IOrderClient ACL, API endpoints | Understanding checkout flow |
| checklist.md | Conformance rules | Code review |
| migration-plan.md | Slice 1 + Slice 2 implementation steps (completed) | Historical reference |
| example-implementation/checkout-confirm-flow.md | POST /confirm full sequence: Presale→Orders→Payments | End-to-end checkout |
| example-implementation/soft-reservation-lifecycle.md | SoftReservation create→confirm→expire lifecycle | Working with reservations |
| example-implementation/price-change-detection.md | GET /price-changes: how stale prices are detected | Price validation logic |

## Key rules
- EC-001 decision: Accept the race condition on concurrent checkout — no distributed lock
- `IOrderClient` is the ACL — Presale never calls Orders BC directly
- Switch complete — no legacy CartController exists

## Related ADRs
- ADR-0011 (Inventory) — soft reservations translate to StockHolds
- ADR-0014 (Orders) — PlaceOrderFromPresaleAsync called via IOrderClient
- ADR-0015 (Payments) — payment initialized on OrderPlaced
'@

Step "ADR-0014 Sales/Orders"
Process-Adr `
  -srcFile         "$adrBase\0014-sales-orders-bc-design.md" `
  -folderPath      "$adrBase\0014" `
  -amendmentRegexes @(
      '^## §16 —',
      '^## §17 —',
      '^## §18 —',
      '^## §19 Design Amendment'
  ) `
  -amendmentNames  @(
      'a1-order-status-lifecycle.md',
      'a2-event-payload-records.md',
      'a3-integration-flow-decisions.md',
      'a4-operator-notifications.md'
  ) `
  -hasMigrationPlan $true `
  -hasChecklist     $true `
  -exampleFiles    @(
      'order-aggregate-usage.md',
      'place-order-flow.md',
      'result-handling-pattern.md',
      'order-product-snapshot.md'
  ) `
  -readmeContent   @'
# ADR-0014: Sales/Orders BC — Order and OrderItem Aggregate Design

**Status**: Accepted — Amended (§16–§19 implemented)
**BC**: Sales/Orders
**Last amended**: 2025-06-27

## What this decision covers
Design of `Order` and `OrderItem` aggregates, TypedIds, result-based error handling,
`OrderCustomer` snapshot, `OrderProductSnapshot`, `OrderEvent` audit log, and cross-BC integration.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0014-sales-orders-bc-design.md | Core design: aggregates §1–§15, Consequences, Alternatives | Understanding Orders BC structure |
| amendments/a1-order-status-lifecycle.md | OrderStatus enum, state transitions, timestamp derivation (overrides §1) | Working with order lifecycle |
| amendments/a2-event-payload-records.md | Event payload record shapes, PaymentId/RefundId moved to payloads | Adding or reading OrderEvents |
| amendments/a3-integration-flow-decisions.md | CartLine cleanup, PaymentConfirmed.Items gap, currency decision | Cross-BC integration questions |
| amendments/a4-operator-notifications.md | OrderRequiresAttention, ShipmentFailurePayload, PartialFulfilmentPayload | Notification/operator message handlers |
| checklist.md | Domain aggregate + infrastructure + application conformance rules | Code review |
| migration-plan.md | 32-step implementation guide (completed) | Historical reference |
| example-implementation/order-aggregate-usage.md | Order.Create(), MarkAsPaid(), Cancel(), AssignCoupon() usage | Implementing order operations |
| example-implementation/place-order-flow.md | PlaceOrderAsync full sequence (Presale→Orders→Payments) | End-to-end order placement |
| example-implementation/result-handling-pattern.md | How to handle PlaceOrderResult / OrderOperationResult in controllers | Writing order controllers |
| example-implementation/order-product-snapshot.md | SnapshotOrderItemsJob + OrderPlacedSnapshotHandler pattern | Working with product snapshots |

## Key rules
- Amendments §16–§19 **override** earlier sections — always check amendments before main ADR
- Order state changes ONLY via domain methods — `order.IsPaid = true` is a compile error after switch
- Switch complete — legacy `OrderService`, `OrderItemService`, `OrderRepository` do not exist

## Related ADRs
- ADR-0015 (Payments) — OrderPlaced triggers PaymentInitialized
- ADR-0026 (Saga) — OrderPlacementFailed compensation
- ADR-0012 (Presale) — PlaceOrderFromPresaleAsync caller
- ADR-0016 (Coupons) — AssignCoupon / RemoveCoupon on Order
'@

Step "ADR-0015 Sales/Payments"
# NOTE: 0015 migration plan is embedded as ### 12. inside Decision — extracted separately below
Process-Adr `
  -srcFile         "$adrBase\0015-sales-payments-bc-design.md" `
  -folderPath      "$adrBase\0015" `
  -amendmentRegexes @() `
  -amendmentNames  @() `
  -hasMigrationPlan $false `
  -hasChecklist     $true `
  -exampleFiles    @(
      'payment-state-machine.md',
      'payment-window-expiry-flow.md',
      'order-cancel-flow.md'
  ) `
  -readmeContent   @'
# ADR-0015: Sales/Payments BC

**Status**: Accepted
**BC**: Sales/Payments
**Last amended**: — (### 12. Migration plan is inside Decision section — extracted separately)

## What this decision covers
Design of the `Payment` aggregate state machine, `OrderPlacedHandler`, `PaymentWindowExpiredJob`,
`Order.Cancel()` extension, and the payments DB schema.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0015-sales-payments-bc-design.md | Core design: §1–§11 Payment aggregate, handlers, job, Order extensions | Understanding Payments BC |
| checklist.md | Conformance rules for Payment aggregate and handlers | Code review |
| example-implementation/payment-state-machine.md | Payment status transitions: Pending→Confirmed→Cancelled | Working with payment lifecycle |
| example-implementation/payment-window-expiry-flow.md | PaymentWindowExpiredJob timing, Order.Cancel() trigger | Debugging payment timeouts |
| example-implementation/order-cancel-flow.md | Order.Cancel() domain method + OrderCancelled message publishing | Implementing cancellation |

## Key rules
- `Payment` state transitions use result types — never throw for expected outcomes
- `PaymentWindowExpiredJob` is owned by Payments BC, registered in TimeManagement
- Switch complete — legacy `PaymentService` and `PaymentHandler` do not exist

## Related ADRs
- ADR-0014 (Orders) — Order.Cancel() added for Payments BC
- ADR-0026 (Saga) — Payment.Cancel() compensation on OrderPlacementFailed
- ADR-0009 (TimeManagement) — PaymentWindowExpiredJob scheduling
'@

# ADR-0015 special: ### 12. Migration plan is a H3 inside Decision — extract manually
$p15file  = "$adrBase\0015\0015-sales-payments-bc-design.md"
$p15lines = [System.IO.File]::ReadAllLines($p15file, [System.Text.Encoding]::UTF8)
$mp15 = Get-Section $p15lines '^### 12\. Migration plan'
if ($mp15) {
    Write-UTF8 "$adrBase\0015\migration-plan.md" $mp15
    $p15cleaned = Remove-Sections $p15lines @('^### 12\. Migration plan')
    $p15cleaned = Trim-Lines $p15cleaned
    [System.IO.File]::WriteAllLines($p15file, $p15cleaned, [System.Text.Encoding]::UTF8)
    OK "Extracted 0015 ### 12. Migration plan"
} else { Warn "0015 ### 12. Migration plan not found" }

Step "ADR-0016 Sales/Coupons"
Process-Adr `
  -srcFile         "$adrBase\0016-sales-coupons-bc-design.md" `
  -folderPath      "$adrBase\0016" `
  -amendmentRegexes @(
      '^## §10 Design Amendments'
  ) `
  -amendmentNames  @(
      'a1-oversize-guard-and-catalog-name-sync.md'
  ) `
  -hasMigrationPlan $true `
  -hasChecklist     $true `
  -exampleFiles    @(
      'apply-coupon-flow.md',
      'new-coupon-rule-guide.md',
      'coupon-evaluation-context.md'
  ) `
  -readmeContent   @'
# ADR-0016: Sales/Coupons BC

**Status**: Accepted — Amended
**BC**: Sales/Coupons
**Last amended**: 2025-06-27

## What this decision covers
Design of `Coupon` aggregate, `CouponUsed` entity, rule-based coupon policy engine (Slice 2),
multi-coupon stacking strategy, and Catalog name sync.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0016-sales-coupons-bc-design.md | Core design: §1–§9 Coupon aggregate + Slice 2 rule pipeline | Understanding Coupons BC |
| amendments/a1-oversize-guard-and-catalog-name-sync.md | §10.1 CouponOversizeGuard constraint rule + §10.2 Catalog→Coupons name sync | Working with oversize guard or name sync handlers |
| checklist.md | Conformance rules | Code review |
| migration-plan.md | Implementation steps (completed) | Historical reference |
| example-implementation/apply-coupon-flow.md | ApplyCouponAsync multi-coupon stacking: Rule A + Rule B | Implementing coupon application |
| example-implementation/new-coupon-rule-guide.md | How to add a new ICouponRule evaluator | Extending the rules engine |
| example-implementation/coupon-evaluation-context.md | CouponEvaluationContext structure and usage | Writing rule evaluators |

## Key rules
- Max coupons per order: default 5, ceiling 10 (`CouponsOptions.MaxCouponsPerOrder`) — see also copilot-instructions.md §7
- Amendment §10.1: `CouponOversizeGuard` is always-on; `BypassOversizeGuard` per-coupon override
- Switch complete — legacy coupon DI removed

## Related ADRs
- ADR-0014 (Orders) — Order.AssignCoupon() / RemoveCoupon()
- ADR-0007 (Catalog) — ProductNameChanged / CategoryNameChanged / TagNameChanged messages
'@

Step "ADR-0017 Sales/Fulfillment"
Process-Adr `
  -srcFile         "$adrBase\0017-sales-fulfillment-bc-design.md" `
  -folderPath      "$adrBase\0017" `
  -amendmentRegexes @(
      '^## §13 Design Amendments'
  ) `
  -amendmentNames  @(
      'a1-shipment-integration-and-fanout.md'
  ) `
  -hasMigrationPlan $true `
  -hasChecklist     $true `
  -exampleFiles    @(
      'refund-lifecycle-flow.md',
      'shipment-dispatch-flow.md'
  ) `
  -readmeContent   @'
# ADR-0017: Sales/Fulfillment BC

**Status**: Accepted — Amended
**BC**: Sales/Fulfillment
**Last amended**: 2025-06-27

## What this decision covers
Design of `Refund` aggregate (Slice 1: request/approve/reject), `Shipment` entity (Slice 2:
dispatch/deliver/fail), cross-BC coordination, and Payments BC extension for refund processing.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0017-sales-fulfillment-bc-design.md | Core design: §1–§12 Refund + Shipment aggregates | Understanding Fulfillment BC |
| amendments/a1-shipment-integration-and-fanout.md | §13.1–13.5: PartiallyDelivered status, enriched messages, parallel fan-out, idempotency | Working with shipment integration |
| checklist.md | Conformance rules | Code review |
| migration-plan.md | Implementation steps (completed) | Historical reference |
| example-implementation/refund-lifecycle-flow.md | RequestRefund → ApproveRefund → RefundApproved message flow | Implementing refund operations |
| example-implementation/shipment-dispatch-flow.md | Shipment dispatch → deliver/fail → fan-out to Orders + Inventory | Working with shipment lifecycle |

## Key rules
- Amendment §13.3: fan-out is parallel — Fulfillment publishes to BOTH Orders and Inventory on shipment events
- `IPaymentService.ProcessRefundAsync` is an extension added to Payments BC — defined in ADR-0015
- Switch complete for both Slice 1 and Slice 2

## Related ADRs
- ADR-0015 (Payments) — ProcessRefundAsync
- ADR-0011 (Inventory) — subscribes to RefundApproved, ShipmentDispatched
- ADR-0014 (Orders) — subscribes to shipment events
'@

# ════════════════════════════════════════════════════════════════════════════
# TIER 2 — Move + README + examples + strip Implementation Status only
# ════════════════════════════════════════════════════════════════════════════

Step "ADR-0003 Feature Folder Organization"
Process-Adr `
  -srcFile         "$adrBase\0003-feature-folder-organization-for-new-bounded-context-code.md" `
  -folderPath      "$adrBase\0003" `
  -amendmentRegexes @() -amendmentNames @() `
  -hasMigrationPlan $false -hasChecklist $false `
  -exampleFiles    @('bc-folder-structure-example.md') `
  -readmeContent   @'
# ADR-0003: Feature Folder Organization for New BC Code

**Status**: Accepted
**BC**: All BCs (structural rule)

## What this decision covers
Canonical folder layout for any new bounded context: where Domain, Application,
Infrastructure, and Web layers live, and how to name things.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0003-feature-folder-organization-for-new-bounded-context-code.md | Folder rules, naming conventions, layer boundaries | Creating any new BC file |
| example-implementation/bc-folder-structure-example.md | Concrete folder tree for a sample BC | Setting up a new BC |

## Key rules
- All new BC code goes in `Domain/{BC}/`, `Application/{BC}/`, `Infrastructure/{BC}/`, `Web/Areas/{BC}/`
- No cross-BC folder references — each BC is a silo

## Related ADRs
- ADR-0004 (module taxonomy) — which BCs exist and their grouping
- ADR-0013 (DbContext interfaces) — Infrastructure layer rules
'@

Step "ADR-0006 TypedId and Value Objects"
Process-Adr `
  -srcFile         "$adrBase\0006-typedid-and-value-objects-as-shared-domain-primitives.md" `
  -folderPath      "$adrBase\0006" `
  -amendmentRegexes @() -amendmentNames @() `
  -hasMigrationPlan $false -hasChecklist $false `
  -exampleFiles    @('typedid-usage-examples.md', 'value-object-patterns.md') `
  -readmeContent   @'
# ADR-0006: TypedId and Value Objects as Shared Domain Primitives

**Status**: Accepted
**BC**: Shared / Domain primitives

## What this decision covers
`TypedId<T>` sealed record base, per-BC typed IDs (`OrderId`, `PaymentId`, etc.),
and shared value objects: `Price`, `Money`, `Quantity`, `StockQuantity`.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0006-typedid-and-value-objects-as-shared-domain-primitives.md | Full design: TypedId pattern, VO invariants, EF conversions | Creating a new TypedId or VO |
| example-implementation/typedid-usage-examples.md | How to define and use a TypedId | Adding a new typed ID |
| example-implementation/value-object-patterns.md | Price, Money, Quantity creation and EF mapping | Working with shared VOs |

## Key rules
- New BC-specific IDs extend `TypedId<int>` (not raw int/Guid)
- `Price` and `Money` require `> 0`; `Quantity` requires `>= 0`
- EF conversions registered in each BC's DbContext configuration

## Related ADRs
- ADR-0003 (folder structure) — where TypedIds live per BC
'@

Step "ADR-0007 Catalog BC"
Process-Adr `
  -srcFile         "$adrBase\0007-catalog-bc-product-category-tag-aggregate-design.md" `
  -folderPath      "$adrBase\0007" `
  -amendmentRegexes @() -amendmentNames @() `
  -hasMigrationPlan $false -hasChecklist $false `
  -exampleFiles    @('product-aggregate-usage.md') `
  -readmeContent   @'
# ADR-0007: Catalog BC — Product, Category, Tag Aggregate Design

**Status**: Accepted
**BC**: Catalog

## What this decision covers
Design of `Product`, `Category`, `Tag` aggregates, image soft-delete, `IImageService`,
and Catalog→other BC name-sync messages.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0007-catalog-bc-product-category-tag-aggregate-design.md | Full design: aggregates, image handling, DB schema | Understanding Catalog BC |
| example-implementation/product-aggregate-usage.md | Product.Create(), Publish/Unpublish, image assignment | Implementing catalog operations |

## Key rules
- Images are soft-deleted — never hard-delete (snapshot URLs remain valid)
- `Product.Quantity` does NOT exist in Catalog — that belongs to Inventory
- Switch complete — legacy Item/Type/Image controllers removed

## Related ADRs
- ADR-0011 (Inventory) — subscribes to ProductPublished/Unpublished
- ADR-0016 (Coupons) — subscribes to ProductNameChanged/CategoryNameChanged/TagNameChanged
'@

Step "ADR-0008 Supporting/Currencies"
Process-Adr `
  -srcFile         "$adrBase\0008-supporting-currencies-bc-design.md" `
  -folderPath      "$adrBase\0008" `
  -amendmentRegexes @() -amendmentNames @() `
  -hasMigrationPlan $false -hasChecklist $false `
  -exampleFiles    @('nbp-api-integration.md') `
  -readmeContent   @'
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
'@

Step "ADR-0013 Per-BC DbContext Interfaces"
Process-Adr `
  -srcFile         "$adrBase\0013-per-bc-dbcontext-interfaces.md" `
  -folderPath      "$adrBase\0013" `
  -amendmentRegexes @() -amendmentNames @() `
  -hasMigrationPlan $false -hasChecklist $false `
  -exampleFiles    @('dbcontext-interface-pattern.md') `
  -readmeContent   @'
# ADR-0013: Per-BC DbContext Interfaces

**Status**: Accepted
**BC**: All BCs (infrastructure rule)

## What this decision covers
Each BC exposes an `IXxxDbContext` interface in the Infrastructure layer.
Repositories depend on the interface, not the concrete `DbContext`.
Enables easy testing and decoupling.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0013-per-bc-dbcontext-interfaces.md | Full design: interface shape, DI alias registration, 27 repos updated | Creating a new repository |
| example-implementation/dbcontext-interface-pattern.md | IXxxDbContext definition + DI alias registration | Adding a new BC repository |

## Key rules
- Repositories MUST inject `IXxxDbContext`, never the concrete class
- DI alias: `services.AddScoped<IXxxDbContext>(sp => sp.GetRequiredService<XxxDbContext>())`
- All 10 BC DbContexts have interfaces — completed

## Related ADRs
- ADR-0003 (folder structure) — where interfaces live
'@

Step "ADR-0021 Frontend Error Pipeline"
Process-Adr `
  -srcFile         "$adrBase\0021-frontend-error-pipeline-and-js-migration-strategy.md" `
  -folderPath      "$adrBase\0021" `
  -amendmentRegexes @() -amendmentNames @() `
  -hasMigrationPlan $false -hasChecklist $false `
  -exampleFiles    @('fetch-first-pattern.md') `
  -readmeContent   @'
# ADR-0021: Frontend Error Pipeline and JS Migration Strategy

**Status**: Accepted
**BC**: Web (frontend)

## What this decision covers
`ExceptionResponse` + `errors.js` pipeline, fetch-first new-code policy,
AMD module cleanup, `addObjectPropertiesToGlobal` removal, and `DOMInitialized` event-data pattern.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0021-frontend-error-pipeline-and-js-migration-strategy.md | Full design: error pipeline phases 1–4, JS migration rules | Writing new JS or handling AJAX errors |
| example-implementation/fetch-first-pattern.md | How to write fetch-first JS (replacing jQuery AJAX) | Writing new frontend JS |

## Key rules
- New JS code uses `fetch` + `errors.js` pipeline — no new jQuery AJAX calls
- `showErrorFromResponse` handles both structured `data.codes` and flat `data.response`
- Phase 4 complete: BS5 modalService rewritten, AMD cleanup done

## Related ADRs
- ADR-0023 (Bootstrap 5) — modalService depends on BS5 API
'@

Step "ADR-0022 Navbar Two-Tier Redesign"
Process-Adr `
  -srcFile         "$adrBase\0022-navbar-two-tier-redesign.md" `
  -folderPath      "$adrBase\0022" `
  -amendmentRegexes @() -amendmentNames @() `
  -hasMigrationPlan $false -hasChecklist $false `
  -exampleFiles    @('navbar-razor-partial.md') `
  -readmeContent   @'
# ADR-0022: Navbar Two-Tier Redesign

**Status**: Accepted
**BC**: Web (frontend)

## What this decision covers
Top navigation bar (search + category filter + cart badge + user menu) and
secondary nav (Kategorie for guests; management bar for MaintenanceRole).

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0022-navbar-two-tier-redesign.md | Full design: two-tier layout, Razor partials, role-based display | Modifying navigation |
| example-implementation/navbar-razor-partial.md | Razor partial structure for navbar tiers | Editing _Layout.cshtml or navbar partials |

## Key rules
- `_LoginPartial.cshtml` is retired — user menu lives in top navbar
- Management bar visible only to `MaintenanceRole`
- Cart badge uses AJAX polling — do not replace with SSE without a new ADR

## Related ADRs
- ADR-0023 (Bootstrap 5) — navbar uses BS5 classes
'@

Step "ADR-0023 Bootstrap 5 Upgrade"
Process-Adr `
  -srcFile         "$adrBase\0023-bootstrap-5-upgrade.md" `
  -folderPath      "$adrBase\0023" `
  -amendmentRegexes @() -amendmentNames @() `
  -hasMigrationPlan $false -hasChecklist $false `
  -exampleFiles    @('bs5-component-migration.md') `
  -readmeContent   @'
# ADR-0023: Bootstrap 5 Upgrade

**Status**: Accepted
**BC**: Web (frontend)

## What this decision covers
Migration from Bootstrap 4 to Bootstrap 5.3.3, TomSelect 2.4.1 installation,
`modalService` rewrite for BS5 API, and removal of BS4 jQuery plugin calls.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0023-bootstrap-5-upgrade.md | Full migration: BS5 changes, modalService rewrite, TomSelect | Writing new views or editing existing BS components |
| example-implementation/bs5-component-migration.md | Before/after BS4→BS5 component examples | Migrating a remaining BS4 component |

## Key rules
- No BS4 `data-toggle`, `data-dismiss`, `data-target` attributes — use `data-bs-*`
- `modalService` uses BS5 `bootstrap.Modal` API — not jQuery `.modal()`
- TomSelect replaces Select2 for all `<select>` enhancements

## Related ADRs
- ADR-0021 (frontend pipeline) — modalService error handling
- ADR-0022 (navbar) — navbar uses BS5 classes
'@

Step "ADR-0024 Controller Routing Strategy"
Process-Adr `
  -srcFile         "$adrBase\0024-controller-routing-strategy.md" `
  -folderPath      "$adrBase\0024" `
  -amendmentRegexes @() -amendmentNames @() `
  -hasMigrationPlan $false -hasChecklist $false `
  -exampleFiles    @('area-routing-examples.md') `
  -readmeContent   @'
# ADR-0024: Controller Routing Strategy

**Status**: Accepted
**BC**: Web (routing rule for all BCs)

## What this decision covers
Convention for `[Area]`, `[Route]`, and `[HttpGet/Post]` attributes across all BC controllers.
All new BC controllers live under `Web/Areas/{BC}/Controllers/`.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0024-controller-routing-strategy.md | Full routing rules: area convention, attribute routing, action naming | Creating a new controller or action |
| example-implementation/area-routing-examples.md | Route attribute examples for standard CRUD actions | Writing new controller actions |

## Key rules
- All BC controllers: `[Area("{BC}")]` + `[Route("[area]/[controller]/[action]")]`
- Action names follow: Index, Create, Edit, Details, Delete convention
- No legacy `Controllers/` folder controllers remain

## Related ADRs
- ADR-0003 (folder structure) — where controllers live
'@

# ════════════════════════════════════════════════════════════════════════════
# TIER 3 — Simple move + README (no section extraction)
# ════════════════════════════════════════════════════════════════════════════

Step "ADR-0001 Project Overview"
Move-Simple `
  "$adrBase\0001-project-overview-and-technology-stack.md" `
  "$adrBase\0001" `
  @'
# ADR-0001: Project Overview and Technology Stack

**Status**: Accepted
**BC**: All (project-wide)

## What this decision covers
Technology stack selection: ASP.NET Core, EF Core, FluentValidation, AutoMapper, xUnit,
MSSQL, Bootstrap, jQuery. Starting point for understanding what this project uses.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0001-project-overview-and-technology-stack.md | Stack decisions, project structure overview | First read for new developers |

## Related ADRs
- ADR-0002 (architecture strategy) — why the current migration is happening
'@

Step "ADR-0002 Post-Event-Storming Strategy"
Move-Simple `
  "$adrBase\0002-post-event-storming-architectural-evolution-strategy.md" `
  "$adrBase\0002" `
  @'
# ADR-0002: Post-Event-Storming Architectural Evolution Strategy

**Status**: Accepted
**BC**: All (migration strategy)

## What this decision covers
Parallel Change strategy, BC isolation rules, atomic switch policy,
and the 80–95% completion gate before atomic switches.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0002-post-event-storming-architectural-evolution-strategy.md | Migration strategy, BC isolation rules, switch policy | Before editing any BC boundary or migration sequencing |

## Key rules
- Atomic switches deferred until 80–95% of BC implementations complete
- Legacy code untouched until switch — parallel change only
- MUST read project-state.md before any BC edit

## Related ADRs
- ADR-0003 (folder structure) — implements this strategy
'@

Step "ADR-0004 Module Taxonomy"
Move-Simple `
  "$adrBase\0004-module-taxonomy-and-bounded-context-grouping.md" `
  "$adrBase\0004" `
  @'
# ADR-0004: Module Taxonomy and Bounded Context Grouping

**Status**: Accepted
**BC**: All (taxonomy reference)

## What this decision covers
Canonical list of all BCs, their group (Sales, Supporting, Identity, Presale, Inventory),
and canonical folder names.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0004-module-taxonomy-and-bounded-context-grouping.md | BC list, groups, folder names, greenfield vs migration | Finding the right BC folder name |

## Related ADRs
- ADR-0003 (folder structure) — how each BC is laid out internally
'@

Step "ADR-0005 AccountProfile BC"
Move-Simple `
  "$adrBase\0005-accountprofile-bc-userprofile-aggregate-design.md" `
  "$adrBase\0005" `
  @'
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
'@

Step "ADR-0018 Supporting/Communication"
Move-Simple `
  "$adrBase\0018-supporting-communication-bc-design.md" `
  "$adrBase\0018" `
  @'
# ADR-0018: Supporting/Communication BC

**Status**: Accepted
**BC**: Supporting/Communication

## What this decision covers
`INotificationService` stub, `IOrderUserResolver`, and the 7 notification handlers
for order/payment/refund lifecycle events.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0018-supporting-communication-bc-design.md | Full design: notification handlers, INotificationService, DI wiring | Adding a new notification handler |

## Key rules
- `LoggingNotificationService` is the current implementation (stub)
- All handlers are in `Application/Supporting/Communication/Handlers/`

## Related ADRs
- ADR-0010 (message broker) — handlers subscribe via IMessageHandler<T>
'@

Step "ADR-0019 Identity/IAM"
Move-Simple `
  "$adrBase\0019-identity-iam-bc-design.md" `
  "$adrBase\0019" `
  @'
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
'@

Step "ADR-0020 Backoffice BC"
Move-Simple `
  "$adrBase\0020-backoffice-bc-design.md" `
  "$adrBase\0020" `
  @'
# ADR-0020: Backoffice BC

**Status**: Accepted
**BC**: Backoffice

## What this decision covers
9 read-only aggregation services delegating to per-BC services,
9 controllers in `Areas/Backoffice`, 21 Razor views, `ManagingRole` authorization.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0020-backoffice-bc-design.md | Full design: service aggregation pattern, controller list, authorization | Working with backoffice features |

## Key rules
- Backoffice services have NO domain model — they delegate to per-BC services only
- All controllers: `[Authorize(Roles = ManagingRole)]`
- Read-only — no write operations in Backoffice BC

## Related ADRs
- ADR-0024 (routing) — Areas/Backoffice controller routing
'@

Step "ADR-0025 API Tiered Access"
Move-Simple `
  "$adrBase\0025-api-tiered-access-trusted-purchase-policy.md" `
  "$adrBase\0025" `
  @'
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
'@

Step "ADR-0026 Order Lifecycle Saga"
Move-Simple `
  "$adrBase\0026-order-lifecycle-saga.md" `
  "$adrBase\0026" `
  @'
# ADR-0026: Order Lifecycle Saga

**Status**: Accepted
**BC**: Cross-BC (Sales/Orders, Sales/Payments, Inventory, Presale)

## What this decision covers
Option A compensation saga for failed order placement:
`OrderPlacementFailed` message + 3 handlers (Payments cancel, Inventory release, Presale cleanup).

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0026-order-lifecycle-saga.md | Full saga design: Option A, compensation handlers, Payment.Cancel() | Working with order placement failure handling |

## Key rules
- Saga uses compensation (rollback) not forward recovery
- `Payment.Cancel()` + `PaymentStatus.Cancelled` added for this saga
- 6 cross-BC integration tests cover the fan-out: `OrderPlacementFailedFanOutTests`

## Related ADRs
- ADR-0014 (Orders) — OrderPlacementFailed publisher
- ADR-0015 (Payments) — Payment.Cancel() compensation
- ADR-0011 (Inventory) — reservation release compensation
- ADR-0012 (Presale) — cart/reservation cleanup compensation
'@

# ════════════════════════════════════════════════════════════════════════════
Done "ADR restructure complete"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Update docs-index.instructions.md — point entries to folder READMEs"
Write-Host "  2. Update .github/context/project-state.md links"
Write-Host "  3. Fill in example-implementation/ stub files with content from main ADRs"
Write-Host "  4. Find old flat ADR links: Get-ChildItem .github,docs -Recurse -Filter *.md | Select-String -Pattern 'docs/adr/\d{4}-'"
Write-Host ""
