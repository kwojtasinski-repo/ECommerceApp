# Research 2026-10-14: wzorce agentowe (Brad Murry + AutoGen + inne) i debata multi-agent

Workflow: `research-gather` (Faza 1 brief → Faza 2 plan + gatherery → Faza 3 merge/weryfikacja), potem debata (wzorzec Debate/Consensus). Typ: external, actor-agnostic.

| Folder / plik | Zawartość |
|---|---|
| `planning/` | `research-brief.md`, `research-plan.md`, `sources.md` |
| `analysis/sources-raw/` | **tylko README**: 43 pliki pełnego tekstu źródeł istnieją wyłącznie lokalnie i nie są w repozytorium (zob. [`SOURCES.md`](SOURCES.md)); pierwotnie: pełny tekst źródeł (wklejone przez użytkownika lub pobrane `curl`em; w nagłówkach proweniencja i URL) |
| `analysis/findings/` | findings z cytatami; `*-raw-*` = z pełnego tekstu (obowiązujące), `bm-agents-fsm`, `bm-data-infra-ops`, `autogen-patterns` = pierwsza tura z WebFetch (zastąpione, zostają jako ślad audytu), `medium-12-patterns` i `mastra-book` = luki |
| `analysis/00-summary.md` | podsumowanie i katalog 13 wzorców + AutoGen |
| `analysis/01-cross-source-verification.md` | sprzeczności, korelacje, werdykty twierdzeń, weryfikacja cytatów (94,8% dosłownie zgodnych) |
| `analysis/02-rejected-information.md` | odrzucone informacje |
| `debate/positions/`, `debate/critiques/` | runda 1: 3 stanowiska i 3 krytyki (pro / con / prag) |
| `debate/judge-round1.md`, `debate/decision.md` | werdykt sędziego (`consensus`) i uzgodniona decyzja z zachowanym zdaniem odrębnym |
| `analysis/sources-index.md` | indeks proweniencji: każdy plik surowy ↔ URL/opis ↔ czy był w pierwotnej liście ↔ jak pozyskano (generowany z nagłówków) |
| [`../../reports/2026-10-14-agent-patterns-workflow-improvements.md`](../../reports/2026-10-14-agent-patterns-workflow-improvements.md) | **raport końcowy**: gdzie wzorce poprawią nasz workflow (z dowodami z tej sesji i referencjami) |
| [`../../reports/2026-10-14-agent-patterns-fit-tradeoffs-guardrails.md`](../../reports/2026-10-14-agent-patterns-fit-tradeoffs-guardrails.md) | 13 wzorców vs nasz setup (co mamy, gdzie warto, gdzie nie), kompromisy i wspólne guardrails G1–G11; pomysły i uwagi użytkownika |
| [`../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md`](../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md) | przegląd raportu (assumption-check + 2 symulacje) i punkty weryfikacji kierunku H1–H9 z regułą GO/ADJUST/PIVOT |
| `application-ideas.md` | pomysły zastosowań: aplikacja ECommerceApp (A1–A6) i setup Claude Code/Copilot (B1–B8), z miarami i kryteriami „stop"; hipotezy do testu |
| `../2026-10-14-llm-self-research/README.md` | dodatkowy self research (LLM-as-judge, reflection, prompt optimization, SLM, caching, pamięć, agent loop, czatbot, nauka modeli) |

**Ograniczenia:** Medium (HTTP 403) i Mastra (treść za bramką e-mail) = luki; 8 artykułów serii i 13 wzorców to rozszerzenie zakresu poza pierwotne 18 URL; konsensus debaty nie jest niezależnym potwierdzeniem (jedna baza dowodów, dominuje jeden autor).

**Aktualizacja 2026-10-15:** dobór wzorca jako reguła z cech, symulacja na historii git i sprawdzenie hipotez: [`../2026-10-15-pattern-selection/README.md`](../2026-10-15-pattern-selection/README.md).

---

## Dla agenta weryfikującego ten research na kodzie

Cel tego folderu: **solidny punkt startowy**, nie gotowa decyzja. Agent, który go dostaje, ma sprawdzić na kodzie, co z tego jest trafne dla ECommerceApp.

- **Pełna bibliografia i proweniencja:** [`SOURCES.md`](SOURCES.md) (z adresami URL, datami dostępu i zakresem odczytu). Kopii cudzych artykułów w repozytorium nie ma.
- **Najpierw przeczytaj:** [`../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md`](../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md) (hipotezy H1–H9, stan GO/ADJUST/PIVOT) i [`debate/decision.md`](debate/decision.md) razem z erratum.
- **Co już zweryfikowano na kodzie (2026-10-14/15):** liczby w `.github/setup-state.md`; brak integracji LLM w kodzie sklepu; `Product` nie ma SKU ani URL (jest `Name`, `Cost`); `IOrderAccessService` działa po tokenie bez `ClaimsPrincipal`; 9 z 10 reguł BLOCKS MERGE bez naruszeń w skanie regexem.
- **Do sprawdzenia na kodzie (otwarte):**
  1. Czy 8 linii rejestracji usług w `ECommerceApp.Web/Startup.cs` i `ECommerceApp.API/Startup.cs` to tolerowany wyjątek, czy naruszenie reguły z `dotnet.instructions.md` (linie 167–169).
  2. Czy bezpośrednie `fetch(...)` w `cartNotification.js` i `checkout-placeorder.js` jest wyjątkiem od reguły „używaj `ajaxRequest`".
  3. Czy czatbot tylko do odczytu może wywołać `IOrderAccessService` + istniejące zapytania o zamówienie z backendowego proxy (end-to-end).
  4. Które reguły z `anti-patterns-critical.context.md` są już egzekwowane (analizatory, testy, `.editorconfig`); znaleziono tylko `BoundedContextDependencyTests`.
- **Czego nie przyjmować na wiarę:** twierdzeń o runtime „Aegis" i metryk „baseline vs optimum" (jeden autor, bez prób i powtórzeń); liczb progów i budżetów (to propozycje); konsensusu debaty (jedna rodzina modeli, jedna baza dowodów).
- **Znane korekty:** erratum E1 i E2 w `analysis/01-cross-source-verification.md` §6; erratum w `debate/decision.md` (SKU/URL).
