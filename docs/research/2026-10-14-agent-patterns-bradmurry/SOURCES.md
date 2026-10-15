# Źródła i podstawy researchu (bibliografia)

- **Data researchu:** 2026-10-14 do 2026-10-15
- **Zasada:** wszystkie źródła są podane jako linki do oryginałów, z datą dostępu i zakresem odczytu.
- **Powiązane:** [`analysis/sources-index.md`](analysis/sources-index.md) (numer ↔ URL), [self research](../2026-10-14-llm-self-research/README.md) §13, [dobór wzorca](../2026-10-15-pattern-selection/README.md) §2.
- **Daty publikacji:** podane tylko tam, gdzie są na stronie albo w metadanych arXiv. Reszta: „brak danych".
- **Pewność użycia:** **pełny tekst** = czytane w całości; **abstrakt/streszczenie** = tylko abstrakt lub podsumowanie narzędzia pobierającego; **fragment** = wynik wyszukiwarki; **luka** = nie udało się odczytać.

---

## A. Pierwotna lista użytkownika (18 URL-i)

### A1. bradmurry.com (14 URL-i; indeks bloga + 13 postów)

Dostęp: 2026-10-14. Indeks https://bradmurry.com/software/ potwierdził 13 postów.

| Post | URL | Wykorzystanie | Zakres odczytu |
|---|---|---|---|
| Agents in Production (intro) | https://bradmurry.com/software/agents-in-production-intro/ | teza „powłoka deterministyczna", trzy twierdzenia serii | pełny tekst |
| Self-evolving agents (intro) | https://bradmurry.com/software/self-evolving-agents-intro/ | pętla RUN/MEASURE/DISCOVER/BUILD, trzy sygnały, dyscypliny | pełny tekst |
| Reactive Reducer Patterns (indeks 13 wzorców) | https://bradmurry.com/software/reactive-reducer-patterns/ | katalog 13 wzorców i ich ograniczenia | pełny tekst |
| Reactive Reducer Patterns (intro serii) | https://bradmurry.com/software/reactive-reducer-patterns-intro/ | trzy reguły reducera, dwa cele wykonania | pełny tekst |
| Walking a Reentrant Human-in-the-Loop FSM, Step by Step | https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ | akumulacja zamiast czyszczenia, porównania `size()`, parkowanie | pełny tekst |
| Binding LLMs to Finite State Machines Using the Reactive Reducer Pattern | https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ | mikrokroki, DSL, trwałość | pełny tekst |
| sparkctl | https://bradmurry.com/software/sparkctl/ | serwowanie modeli z konfiguracji | pełny tekst |
| DSL / AI partition | https://bradmurry.com/software/dsl-ai-partition/ | podział DSL ↔ AI jako granica uprawnień | pełny tekst |
| AI annotations | https://bradmurry.com/software/ai-annotations/ | adnotacje `@!` | pełny tekst |
| PyFlink (intro) | https://bradmurry.com/software/pyflink-intro/ (2023-07-15 wg strony) | streaming vs micro-batching | pełny tekst |
| API Architecture for Massive Scale | https://bradmurry.com/software/apis-at-massive-scale/ | CDC, pre-computed paths, Redis + S3 | pełny tekst |
| Devops Contract | https://bradmurry.com/software/dev-ops-contract/ | kontrakt komend + faz | pełny tekst |
| Config Driven Data Transforms (pt1) | https://bradmurry.com/software/config-driven-transforms/ | DSL source/sink, walidacja | pełny tekst |
| (indeks bloga) | https://bradmurry.com/software/ | tylko potwierdzenie listy postów | abstrakt/lista |

### A2. Pozostałe z listy

| Źródło | URL | Status |
|---|---|---|
| Medium: Every AI agent needs these 12 design patterns… | https://medium.com/@mohsho10/every-ai-agent-needs-these-12-design-patterns-or-it-will-eventually-fail-4970ebd4cb89 | **luka** (HTTP 403, brak konta; użytkownik zrezygnował) |
| Mastra: Patterns of Building AI Agents | https://mastra.ai/books/patterns-of-building-ai-agents | **luka** (treść rozdziałów za bramką e-mail; odczytany tylko spis treści) |
| AutoGen: design patterns, intro | https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/intro.html | pełny tekst |
| AutoGen: multi-agent debate | https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/multi-agent-debate.html | pełny tekst |

---

## B. Rozszerzenie zakresu: artykuły Brada spoza pierwotnej listy

Odnalezione z linków na stronach indeksowych; **URL-e i tytuły zweryfikowane 2026-10-15 (HTTP 200)**. Czytane 2026-10-14.

### B1. 13 wzorców orkiestracji (jako „Implemented via the Reactive Reducer")
Wszystkie pod `https://bradmurry.com/software/<slug>`:

| # | Wzorzec | slug |
|---|---|---|
| 1 | Sequential Pipeline | `sequential-pipeline-pattern-implemented-via-reactive-reducer` |
| 2 | Parallel Fan-Out / Fan-In | `parallel-fan-out-fan-in-pattern-implemented-via-reactive-reducer` |
| 3 | Supervisor / Router | `supervisor-router-pattern-implemented-via-reactive-reducer` |
| 4 | Hierarchical Manager / Worker | `hierarchical-manager-worker-pattern-implemented-via-reactive-reducer` (2026-09-04 wg strony) |
| 5 | Peer Handoff / Swarm | `peer-handoff-swarm-pattern-implemented-via-reactive-reducer` |
| 6 | Debate / Consensus | `debate-consensus-pattern-implemented-via-reactive-reducer` |
| 7 | Blackboard / Shared-State | `blackboard-shared-state-pattern-implemented-via-reactive-reducer` (2026-09-07) |
| 8 | Planner / Executor | `planner-executor-pattern-implemented-via-reactive-reducer` |
| 9 | Generator / Critic / Refiner | `generator-critic-refiner-pattern-implemented-via-reactive-reducer` |
| 10 | Auction / Contract-Net | `auction-contract-net-pattern-implemented-via-reactive-reducer` (2026-09-10) |
| 11 | Event-Driven / Publish-Subscribe | `event-driven-publish-subscribe-pattern-implemented-via-reactive-reducer` (2026-09-11) |
| 12 | Quorum / Voting | `quorum-voting-pattern-implemented-via-reactive-reducer` (2026-09-12) |
| 13 | Dynamic Team Formation | `dynamic-team-formation-pattern-implemented-via-reactive-reducer` |

Przykład pełnego adresu: https://bradmurry.com/software/debate-consensus-pattern-implemented-via-reactive-reducer

### B2. Serie
| Seria / część | Tytuł | slug |
|---|---|---|
| Self-evolving 1 | Log and Telemetry Triage at Scale | `self-evolving-agents-pt-1` |
| Self-evolving 2 | Data Quality Audits over Messy Data | `self-evolving-agents-pt-2` |
| Self-evolving 3 | Support Ticket Mining at Scale | `self-evolving-agents-pt-3` |
| Self-evolving 4 | Entity Resolution and Data Fusion | `self-evolving-agents-pt-4` |
| Agents in Production 1 | Agents Are Distributed Systems Now | `agents-in-production-pt-1` |
| Agents in Production 2 | What Kafka and Flink Taught Me About Building Reliable AI Agents | `agents-in-production-pt-2` |
| Agents in Production 3 | Deterministic Shell, Probabilistic Core | `agents-in-production-pt-3` |
| Agents in Production 4 | Stop Treating Agent Workflows Like Task Lists | `agents-in-production-pt-4` |

Adres: `https://bradmurry.com/software/<slug>`. Części 5–7 serii „Agents in Production" są tylko wspomniane w tekście, nie czytano ich.

### B3. Druga partia stron AutoGen (dodana przez użytkownika później)
Wszystkie pod `https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/`: `concurrent-agents.html`, `sequential-workflow.html`, `group-chat.html`, `handoffs.html`, `mixture-of-agents.html`, `reflection.html`, `code-execution-groupchat.html`. Pełny tekst, czytane 2026-10-14.

---

## C. Źródła zewnętrzne (self research i dobór wzorca)

Dostęp: 2026-10-14 (część 1) i 2026-10-15 (część 2). Szczegóły i cytaty: [self research §13](../2026-10-14-llm-self-research/README.md), [dobór wzorca §2](../2026-10-15-pattern-selection/README.md).

| Źródło | URL | Data publikacji | Na czym się oparto | Zakres odczytu |
|---|---|---|---|---|
| Anthropic, Building Effective Agents | https://www.anthropic.com/engineering/building-effective-agents | brak danych | workflow vs agent, 5 wzorców, „simplest solution first" | pełny tekst (streszczenie narzędzia) |
| Anthropic, multi-agent research system | https://www.anthropic.com/engineering/multi-agent-research-system | brak danych | reguła skalowania wysiłku, koszt 4×/15×, słabe dopasowania | streszczenie narzędzia |
| Anthropic, prompt caching (dokumentacja) | https://platform.claude.com/docs/en/build-with-claude/prompt-caching | stan 2026-10-14 | ceny, TTL, unieważnianie | dokumentacja (pierwsze 100 tys. znaków) |
| Anthropic, streaming (dokumentacja) | https://platform.claude.com/docs/en/build-with-claude/streaming | stan 2026-10-14 | SSE w czatbocie | dokumentacja (początek) |
| OpenAI, prompt caching (dokumentacja) | https://developers.openai.com/api/docs/guides/prompt-caching | stan 2026-10-14 | minimum, TTL, ceny | dokumentacja |
| OWASP Top 10 for LLM Applications 2025 | https://genai.owasp.org/llm-top-10/ | edycja 2025 | ryzyka czatbota | lista |
| Zheng i in., LLM-as-a-Judge | https://arxiv.org/abs/2306.05685 | 2023-06-09 (v4 2023-12-24) | biasy sędziego, >80% zgodności z ludźmi | abstrakt |
| Gu i in., Survey on LLM-as-a-Judge | https://arxiv.org/abs/2411.15594 | brak danych | taksonomia biasów | fragment |
| LLMs-as-Judges survey | https://arxiv.org/abs/2412.05579 | brak danych | dodatkowe biasy | fragment |
| Shinn i in., Reflexion | https://arxiv.org/abs/2303.11366 | 2023-03-20 (v4 2023-10-10) | pamięć epizodyczna, HumanEval | abstrakt |
| Huang i in., LLMs Cannot Self-Correct Reasoning Yet | https://arxiv.org/abs/2310.01798 | 2023-10-03 (v2 2024-03-14) | samokorekta wymaga zewn. sygnału; debata vs self-consistency | abstrakt; liczby debaty z fragmentów (niska pewność) |
| Madaan i in., Self-Refine | https://arxiv.org/abs/2303.17651 | brak danych | refleksja ze strukturą | fragment |
| Agrawal i in., GEPA | https://arxiv.org/abs/2507.19457 | 2025-07-25 (v2 2026-02-14) | optymalizacja promptów | abstrakt |
| Belcak i in., SLM are the Future of Agentic AI | https://arxiv.org/abs/2506.02153 | 2025-06-02 (v3 2026-09-22) | stanowisko o SLM | abstrakt |
| Packer i in., MemGPT | https://arxiv.org/abs/2310.08560 | 2023-10-12 (v2 2024-02-12) | zarządzanie kontekstem | abstrakt |
| Chhikara i in., Mem0 | https://arxiv.org/abs/2504.19413 | 2025-04-28 | pamięć długoterminowa (deklaracje autorów) | abstrakt |
| Yao i in., ReAct | https://arxiv.org/abs/2210.03629 | 2022-10-06 (v3 2023-03-10) | pętla agenta | abstrakt |
| Liu i in., Agent Design Pattern Catalogue | https://arxiv.org/abs/2405.10467 | 2024-05-16 (v4 2024-11-06) | katalog 18 wzorców, model decyzyjny (§5.1) | HTML v4 (sekcje 4–5.1) |
| Kim i in., Towards a Science of Scaling Agent Systems | https://arxiv.org/abs/2512.08296 | 2025-12-09 (v3 2026-04-08) | predyktor architektury (87%), sekwencyjne vs podzielne | abstrakt; **wersje się rozjeżdżają** |
| Google Research, wpis o skalowaniu systemów agentów | https://research.google/blog/towards-a-science-of-scaling-agent-systems-when-and-why-agent-systems-work/ | brak danych | reguły wyboru i wzmacnianie błędów | streszczenie narzędzia |
| Zhang i in., Stop Overvaluing Multi-Agent Debate… | https://arxiv.org/abs/2502.08788 | 2025-02-12 (v3 2025-06-21) | debata vs proste bazy, heterogeniczność | abstrakt |
| Yue i in., MasRouter (ACL 2025) | https://aclanthology.org/2025.acl-long.757 | 2025 | wyuczony router współpracy | streszczenie z wyszukiwarki |
| Hugging Face PEFT, LoRA | https://huggingface.co/docs/peft/main/en/conceptual_guides/lora | brak danych | LoRA | dokumentacja |
| karpathy/nanoGPT | https://github.com/karpathy/nanoGPT | README z 2025-11 (deprecated) | trening od zera | README |
| karpathy/nanochat | https://github.com/karpathy/nanochat | brak danych | trening od zera | README |

Blogi i tutoriale (porównania hostingu modeli, bezpieczeństwo czatbotów, poradniki LoRA, caching): znalezione wyszukiwarką 2026-10-14, oznaczone w self research jako **niska pewność**; nie są podstawą żadnego wniosku bez potwierdzenia we własnych źródłach.

---

## D. Pliki wewnętrzne repozytorium, na których oparto ocenę setupu

Tylko pliki śledzone w gicie: `.github/AGENT-PIPELINE.md`, `.github/agents/*.md`, `.github/skills/research-gatherer/SKILL.md`, `.github/instructions/{agent-workflow,session-continuity,mcp-routing,docs-index,dotnet}.instructions.md`, `.github/context/{agent-decisions,anti-patterns-critical,repo-index}.context.md`, `.github/context-cost-analysis.md`, `.github/setup-state.md`, `.claude/settings.json`, `.claude/agents/`, `tools/hooks/README.md`, `docs/architecture/bounded-context-map.md`, kod: `ECommerceApp.Domain/Catalog/Products/Product.cs`, `ECommerceApp.Application/Presale/Checkout/Services/IOrderAccessService.cs`, `ECommerceApp.UnitTests/Architecture/`, `ECommerceApp.Web/Startup.cs`, `ECommerceApp.Web/wwwroot/js/`.
Historia git: 200 ostatnich commitów (2026-05-27 do 2026-10-14) do symulacji wielkości zadań.

---

## E. Na czym opierają się najważniejsze wnioski

| Wniosek | Podstawa | Siła |
|---|---|---|
| Bramki dowodowe w researchu dają wartość | pomiar w tej sesji: 5 z 6 odpowiedzi WebFetch fragmentarycznych; 1 373 z 1 448 cytatów zgodnych | zmierzone w tym researchu |
| Krytyk/sędzia LLM wymaga zewnętrznego sygnału i kalibracji | Zheng 2023, Huang 2023, Brad (zgodność tylko kierunkowa) | średnia |
| Debata nie jest domyślnie lepsza niż prosta baza | Zhang 2025 (abstrakt), Huang (fragment) | średnia–niska |
| Koordynacja wieloagentowa szkodzi zadaniom sekwencyjnym | Kim 2025, Anthropic | średnia (rozbieżne wersje Kim) |
| Własna warstwa decyzyjna nie ma przewagi dowodowej nad importem frameworka (ani odwrotnie) | Anthropic (frameworki mogą ukrywać prompty), rozbieżności proza/kod w AutoGen | niska–średnia |
| Czatbot: klucz po stronie serwera, limity, brak narzędzi zapisujących | OWASP LLM Top 10 2025 | wysoka (lista ryzyk) |
| Produkt nie ma SKU ani URL | `Product.cs` w repo | wysoka (kod) |
| Wszystkie twierdzenia o runtime „Aegis" | wyłącznie Brad | niska (jeden autor, brak publicznej weryfikacji) |

---

## F. Cytaty
- W findings są wyłącznie krótkie cytaty (≤ 40 słów) z podaniem źródła; pełne teksty artykułów są dostępne pod linkami powyżej.
