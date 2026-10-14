# Research Findings Summary (Phase 3, step 1)

Data: 2026-10-14 · Typ: external · Actor-agnostic · Brief: [`../planning/research-brief.md`](../planning/research-brief.md)

## Research Question
Zebrać i zweryfikować krzyżowo treść podanych źródeł (14 URL-i bradmurry.com, Medium, Mastra, 2 strony AutoGen) i wskazać wzorce/umiejętności agentowe; docelowo użyć ich w debacie multi-agent o zastosowaniach w AI na produkcji. **Ten dokument jest tylko zbiorem faktów — bez rekomendacji** (rekomendacja = osobny krok, patrz `../debate/`).

## Sources Investigated

| Grupa | Źródła | Pełny tekst? | Pliki surowe | Findings (finalne) |
|---|---|---|---|---|
| Brad: rdzeń agentowy (6 postów z listy) | binding-llms-to-FSM, walking-HIL-FSM, 13-patterns index, agents-in-production-intro, self-evolving-intro, reactive-reducer-intro | tak (wklejone przez użytkownika) | 03–08 | `bm-agentic-core-raw-findings.md` (132 sekcje) + `bm-patterns-1-7-raw` + `bm-patterns-8-13-raw` |
| Brad: 13 artykułów per wzorzec | wzorce 1–13 | tak (wklejone) — **poza listą 18 URL** (rozszerzenie zakresu) | 09–21 | `bm-patterns-1-7-raw` (51), `bm-patterns-8-13-raw` (77) |
| Brad: serie „Self-evolving" 1–4 i „Agents in Production" 1–4 | 8 artykułów | tak (wklejone) — **poza listą 18 URL** | 22–29 | `bm-series-parts-raw` (55) |
| Brad: posty infra z listy | apis-at-massive-scale, dev-ops-contract, config-driven-transforms, sparkctl, dsl-ai-partition, ai-annotations, pyflink-intro | tak (3 wklejone, 4 pobrane `curl`em) | 30–32, 42–45 | `bm-infra-raw` (73) |
| AutoGen (9 stron design-patterns) | intro, concurrent-agents, sequential-workflow, group-chat, handoffs, mixture-of-agents, multi-agent-debate, reflection, code-execution-groupchat | tak (`curl` + konwersja HTML→tekst) | 33–41 | `autogen-raw-9pages` (14 sekcji, ~83 findings) |
| Mastra *Patterns of Building AI Agents* | 1 strona | **nie** — treść za bramką e-mail; tylko spis treści | — | `mastra-book-findings.md` (luka) |
| Medium *12 design patterns* | 1 artykuł | **nie** — HTTP 403, użytkownik zrezygnował | — | `medium-12-patterns-findings.md` (luka) |
| Self research (dodatkowy) | paperty, dokumentacje (caching, OWASP, LoRA…) | abstrakty/dokumentacje | — | [`../../2026-10-14-llm-self-research/README.md`](../../2026-10-14-llm-self-research/README.md) |

Pliki findings z pierwszej tury (z WebFetch, fragmentaryczne) — `bm-agents-fsm`, `bm-data-infra-ops`, `autogen-patterns` — są **zastąpione** przez wersje `*-raw` tam, gdzie istnieje pełny tekst; zostają jako ślad audytu.

## Key Findings (fakty, bez rekomendacji)

### A. Teza wspólna źródeł Brada (reactive reducer)
- Kontekst (typowany store) jest jedynym źródłem prawdy; aktorzy (agent, człowiek, narzędzie, system) tylko *wnoszą wartości*, nigdy nie „żądają przejścia"; maszyna sama wykonuje pierwszą krawędź, której guardy przechodzą (pętla mikrokroków, cap 128, parkowanie, journal). [`03`, `08`]
- Legalność jest strukturalna (niezadeklarowana krawędź nie może odpalić); reducer jest czysty (efekty tylko w akcjach entry/exit/onTransition); guardy to CEL. [`03`]
- Te same definicje FSM mają działać interaktywnie lub skompilowane do Mutiny/Flink. [`03`, `08`]
- Powtarzalne idiomy (13 wzorców): *wielu wnoszących → jedno pole-mapa kluczowane autorem runtime'u*; *accumulate what you keep, consume what re-arms*; *gdy nic nie jest niszczone, każdy test staje się porównaniem* (`size(a) > size(b)`). [`05`, `15`–`17`]
- „Powłoka deterministyczna wokół probabilistycznego rdzenia": wzorce z systemów rozproszonych (idempotency key, journal, replay, checkpoint/savepoint, backpressure, DLQ) przeniesione na agentów; „timeout is UNKNOWN, not FAILED". [`26`–`29`]
- Pętla samo-ewoluującego agenta: RUN → MEASURE → DISCOVER → BUILD; trzy sygnały (efektywność, poprawność, jakość); dyscypliny: jedna zmiana naraz, sprawdź zanim zbudujesz, zgłoś regresję, zgłoś sufit. [`07`, `22`–`25`]

### B. Katalog 13 wzorców (Brad) — mechanizm i stwierdzone ograniczenie

| # | Wzorzec (kategoria) | Kluczowy mechanizm | Ograniczenie podane przez autora |
|---|---|---|---|
| 1 | Sequential Pipeline (Flow) | pole per etap; back-edge konsumuje trigger, zachowuje notatki | `false` jest „present" — re-armowalne pole nie może być bool |
| 2 | Parallel Fan-Out/Fan-In (Flow) | wymagania = fan-out; guard `size(findings) >= N` = join; `required:false` gdy join ≠ all-of | dynamiczny wariant przez `fork()` z ograniczonym re-dispatch |
| 3 | Supervisor/Router (Authority) | enum `route` + hub; `finding` konsumowane, `notes` akumulowane | trasowanie dwa razy do tego samego specjalisty „does nothing", jeśli pole nie skonsumowane |
| 4 | Hierarchical (Authority) | `spawn … alias … into`, `fsmCompleted[alias]`; własny store na poziom | brak deklaratywnego seedowania kontekstu dziecka (brief idzie adresowanym zapisem) |
| 5 | Peer Handoff/Swarm (Delegation) | enum `handoff`, `trail` (lista) przez exit-hook, stan `stuck` | no-progress guard nie obejmuje tur; mesh jest rozwlekły |
| 6 | Debate/Consensus (Reasoning) | 3 debaterzy + sędzia; `positions` zachowane, `critiques` czyszczone; enum `consensus/continue`; `maxIterations: 8` | wspólne ślepe punkty wymagają różnorodności modeli, nie innej pętli; koszt = debaterzy × rundy × (pozycje+krytyki) + sędzia |
| 7 | Blackboard (Coordination) | `facts` (lista, zostaje) vs `hypotheses` (mapa, czyszczona) | kolekcje 1 poziom; schemat stały; brak oportunistycznego schedulera |
| 8 | Planner/Executor (Planning) | `directive`/`plan` osobno; replan konsumuje 3 triggery; `history` | plan to tekst, nie graf zadań |
| 9 | Generator/Critic/Refiner (Reasoning) | brak stanu refine; `size(drafts) < 4` jako budżet; stan `stalled` | brak „apply diff" — refiner re-emituje cały artefakt |
| 10 | Auction/Contract-Net (Delegation) | mapy `bids`/`confidence`; `winner` enum; delegat z szablonu `{{ctx.winner}}` | jednorundowa aukcja; bid to nie rekord |
| 11 | Event-Driven/Pub-Sub (Coordination) | reducer jako wewnętrzna szyna; reaktorzy = wymagania | **„half real"**: `emit` tylko loguje, brak ingress/egress |
| 12 | Quorum/Voting (Reasoning) | mapa `votes` kluczowana autorem; guard `size(filter(...)) >= 2` | jednorazowe; korelacja błędów nie do naprawy mechanizmem |
| 13 | Dynamic Team Formation (Delegation) | `roster` = lista enum (rejestr jako typ); jeden stan `phase`; kolejność = `roster[size(outputs)]` | wymiana członka w trakcie nie działa „za darmo" |

### C. AutoGen — 8 wzorców (core-user-guide)
Concurrent agents, sequential workflow, group chat (manager wybiera mówcę), handoffs (delegate tool zwraca topic), mixture-of-agents (warstwy workerów + agregator, direct messaging), **multi-agent debate**, reflection (coder + reviewer, JSON `approved`), code-execution-groupchat. Szczegóły i cytaty: `findings/autogen-raw-9pages-findings.md`.
- **Debate (AutoGen):** 4 identyczni solverzy + agregator; rzadka topologia pierścienia; „fixed number of rounds" (kod `max_round=3`); agregacja = **głosowanie większościowe** `max(set(answers), key=answers.count)`; **brak roli sędziego/krytyka**; strona nie mówi nic o rozstrzyganiu remisów ani o ewaluacji; jedyny przykład kończy się pełną zgodnością.
- **Różnica kluczowa dla debaty:** Brad = sędzia + enum + krytyka + budżet; AutoGen = równorzędni solverzy + głosowanie + stała liczba rund.

### D. Posty infra/AI (z listy)
- *apis-at-massive-scale*: hash-before-write → czysty CDC → przeliczenie raz przy zmianie → pre-computed ścieżki `/{category}/{type}/{key}` → dual-write Redis + S3 jako fallback. Wynik: samoopis „billions of requests".
- *dev-ops-contract*: kontrakt = interfejs komend (`make deps/test/build/deploy`) + cykl życia faz z short-circuit.
- *config-driven-transforms*: DSL source/sink; walidacja w 4 punktach; wersjonowanie; testy JSON-in/JSON-out.
- *sparkctl*: serwowanie modeli sterowane konfiguracją (`cluster.yaml` + `recipes/*.yaml`, `apply`, wykrywanie driftu sha256, jednolity endpoint zgodny z OpenAI na LiteLLM). Brak opisu wzorca agentowego.
- *dsl-ai-partition*: DSL jako granica uprawnień między orkiestracją pisaną przez AI a krytycznymi komponentami pisanymi przez ludzi; dwie ścieżki zatwierdzania (szybka/ostrożna). Liczby „90%/80%" bez poparcia.
- *ai-annotations*: standard adnotacji `@!`; brak opisu, jak runtime agenta je wczytuje.
- *pyflink-intro*: krótki post — micro-batching ≠ streaming.

### E. Mastra (tylko spis treści — treść rozdziałów niedostępna)
1. From Wishlist to Working Agent (m.in. Human-in-the-Loop) 2. Intro to Context Engineering (compress context, share context between subagents…) 3. From MVP to Production (failure modes, evals, SME labels, production datasets) 4. Autonomy Is a Two-Edged Sword (lethal trifecta, sandbox, access control, guardrails) 5. What's Next(ish).

## Gaps and Uncertainties
- **Medium** — 0 faktów. **Mastra** — tylko tytuły rozdziałów. Wzorce z tych źródeł **nie mogą** wejść do katalogu jako ustalone.
- Wszystkie twierdzenia o runtime'ie „Aegis" pochodzą od jednego autora; **nie znalazłem publicznie zweryfikowanej implementacji** w zebranym materiale (post sparkctl/rejected: wzmianka o repo aegis nie została zweryfikowana).
- Wszystkie metryki „baseline vs optimum" (np. tokeny 612 400 → 26 400) to **samoopisy bez próby, bez liczby powtórzeń i bez informacji, czy to realne uruchomienia** (gatherer `bm-series-parts-raw`).
- Części 5–7 „Agents in Production" i akty II–III są tylko wspomniane, niedostępne.
- Cytaty weryfikowane skryptem — patrz `01-cross-source-verification.md` §5.
