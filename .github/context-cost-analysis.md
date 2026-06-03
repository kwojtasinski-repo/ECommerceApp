# Context Cost Analysis — ECommerceApp

> Generated: 2026-06-03. Re-run the measurement in §5 after any `.github/` change.
> Token estimates use **~4 chars = 1 token** (cl100k_base approximation, ±15% for mixed PL/EN + code).
> Pricing: GitHub usage-based billing from June 1, 2026.

---

## 1. Assumptions

| Assumption | Value | Basis |
|---|---|---|
| 1 AI Credit | = $0.01 USD | GitHub official docs |
| Copilot Business allotment | **1,900 cr/user/month** | Standard from June 1, 2026 |
| Bonus period (Jun–Aug 2026) | **4,900 cr/user/month** | Existing customers: +3,000 cr/month — ends Sept 1, 2026 |
| Included models | **0 cr/token** | GPT-4.1 and GPT-5 mini confirmed as included |
| Token approximation | 4 chars = 1 token | cl100k_base heuristic (±15% for this codebase mix) |

---

## 2. Model rates

### Claude Sonnet 4.6 (premium — consumes credits)

| Token type | Rate / 1M | Credits / token |
|---|---|---|
| Input (fresh) | $3.00 | 0.0003 cr |
| Input (cached) | $0.30 | 0.00003 cr |
| Cache write | $3.75 | 0.000375 cr |
| **Output** | **$15.00** | **0.0015 cr** |

> Output tokens cost **5× more than fresh input**. Minimize unnecessary output verbosity in agentic sessions.

### Included models (zero credits)

| Model | Input fresh | Output |
|---|---|---|
| GPT-4.1 | $2.00/1M | $8.00/1M |
| GPT-5 mini | $0.25/1M | $2.00/1M |

> **Use included models for exploration turns** (reading ADRs, finding files, summarizing context). Reserve Sonnet 4.6 for implementation and complex reasoning.

---

## 3. Fixed cost — every single request

Files auto-loaded on **every** Copilot interaction (`applyTo: "**"` or always-attached root instructions).

| File | ~Chars | ~Tokens | Note |
|---|---|---|---|
| `copilot-instructions.md` | 8,734 | 2,184 | Auto-attached root instructions |
| `instructions/mcp-routing.instructions.md` | 8,231 | 2,058 | `applyTo: "**"` |
| `instructions/pre-edit.instructions.md` | 2,851 | 713 | `applyTo: "**"` |
| `instructions/doc-suggestions.instructions.md` | 3,361 | 840 | `applyTo: "**"` |
| `instructions/batched-tasks.instructions.md` | 2,564 | 641 | `applyTo: "**"` |
| `instructions/safety.instructions.md` | 1,830 | 458 | `applyTo: "**"` |
| `instructions/agent-memory.instructions.md` | 864 | 216 | `applyTo: "**"` |
| **TOTAL fixed** | | **7,110** | |

**Credit cost per turn (fixed block only):**

| Scenario | Credits |
|---|---|
| Cold turn (nothing cached) | 7,110 × 0.0003 = **~2.13 cr** |
| Warm turn (instructions cached) | 7,110 × 0.00003 = **~0.21 cr** |

> ⚠️ At 1,900 cr/month budget: cold turns alone cap out at ~892 turns/month (~30/day).
> Instruction caching is critical — warm turns leave **~1,879 cr** for output.

---

## 4. On-demand instructions — triggered by open file type

| File | Trigger (applyTo glob) | ~Tokens |
|---|---|---|
| `instructions/docs-index.instructions.md` | Loaded by many agents on demand | 1,655 |
| `instructions/copilot-config-sync.instructions.md` | `.github/**`, `docs/**` | 1,501 |
| `instructions/dotnet.instructions.md` | No glob — loaded on agent demand | **4,540** |
| `instructions/frontend.instructions.md` | `ECommerceApp.Web/wwwroot/**`, `**/*.cshtml` | 1,303 |
| `instructions/bc-adr-map.instructions.md` | `**/*.cs`, `**/*.csproj`, `**/*.cshtml` | 170 |
| `instructions/shared-primitives.instructions.md` | `ECommerceApp.Domain/Shared/**/*.cs` | 678 |
| `instructions/efcore.instructions.md` | `ECommerceApp.Infrastructure/**/*.cs` | 523 |
| `instructions/razorpages.instructions.md` | Loaded on agent demand | 632 |
| `instructions/testing.instructions.md` | Loaded on agent demand | 662 |
| `instructions/web-api.instructions.md` | Loaded on agent demand | 641 |
| `instructions/migration-policy.instructions.md` | `ECommerceApp.Infrastructure/Migrations/**` | 357 |

> ⚠️ **`dotnet.instructions.md` at 4,540 tokens is the largest on-demand file.**
> Loading it on a cold turn adds 4,540 × 0.0003 = **~1.36 cr** beyond the fixed block.
> Load it only for deep architectural questions and full code reviews — never for routine tasks.

---

## 5. Context files (loaded on agent/review demand)

| File | ~Tokens | When loaded |
|---|---|---|
| `context/anti-patterns-critical.context.md` | 1,703 | Every `@code-reviewer` turn, `code-validator` skill |
| `context/anti-patterns-advisory.context.md` | 414 | Full `@code-reviewer` deep reviews only |
| `context/project-state.md` | ~400 (est.) | `@code-reviewer`, BC-change checks |
| `context/agent-decisions.md` | varies | Every non-trivial task (RAG-first via `query_docs`) |

---

## 6. Worst-case turn (full code review)

| Component | Tokens |
|---|---|
| Fixed always-loaded block | 7,110 |
| `docs-index.instructions.md` | 1,655 |
| `dotnet.instructions.md` | 4,540 |
| `anti-patterns-critical.context.md` | 1,703 |
| `anti-patterns-advisory.context.md` | 414 |
| One BC instruction (e.g. efcore) | 523 |
| **TOTAL worst-case input** | **15,945** |

Cold worst-case: 15,945 × 0.0003 = **~4.78 cr/turn**
Warm worst-case: 15,945 × 0.00003 = **~0.48 cr/turn**

---

## 7. Budget tips

1. **Use included models for exploration** — `list_adrs()`, `query_docs()`, reading files → GPT-4.1 (0 credits).
2. **Keep `dotnet.instructions.md` out of routine turns** — load only for full reviews and architectural decisions.
3. **RAG-first for ADR lookups** — `get_history(id)` instead of `read_file` on `docs/adr/**` avoids 1–3 K tokens per ADR.
4. **Instruction caching pays off** — after the first turn, subsequent turns with the same instructions are 10× cheaper on input.
5. **`mcp-routing.instructions.md` is the largest always-loaded file (2,058 tokens)** — the high-frequency routing rules justify the cost, but avoid redundant duplication in `copilot-instructions.md`.

---

## 8. Re-run measurement

```powershell
$files = Get-ChildItem "c:\Projekty\ECommerceApp\.github" -Recurse -Filter "*.md" |
    Where-Object { $_.FullName -notmatch "\\hooks\\" }
$files | ForEach-Object {
    $chars = (Get-Content $_.FullName -Raw).Length
    [PSCustomObject]@{
        File   = $_.FullName.Replace("c:\Projekty\ECommerceApp\.github\", "")
        Chars  = $chars
        Tokens = [math]::Round($chars / 4)
    }
} | Sort-Object Tokens -Descending | Format-Table -AutoSize
```
