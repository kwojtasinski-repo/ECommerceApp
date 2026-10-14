# Raport końcowy: jak wzorce agentowe mogą ulepszyć NASZ workflow (research, planowanie, walidacja)

- **Data:** 2026-10-14
- **Status:** raport z researchu i z jednej próby w praktyce. **Wnioski o tym, co działa, opieram na tej sesji (n = 1, bez grupy kontrolnej).** Rzeczy oznaczone „PILOT" to hipotezy do sprawdzenia, nie ustalenia.
- **Dla kogo / po co:** żeby za jakiś czas dało się odtworzyć, **skąd się wzięły te pomysły i co je poparło** (patrz §6 Referencje i proweniencja).
- **Zakres tego raportu:** ulepszanie **sposobu pracy** (workflow research-gather, planowanie/walidacja zmian, decyzje ADR). Pomysły dotyczące samej aplikacji sklepu są osobno: [`application-ideas.md`](../research/2026-10-14-agent-patterns-bradmurry/application-ideas.md) (część A) i zostają tam jako lista pomysłów.
- **Uwaga o zapisie:** wszystkie zmiany workflow z §3 wymagają edycji poza `docs/research/**` i `docs/reports/**`, więc wymagają **`IMPLEMENT`** (GO gate). Ten raport niczego w workflow nie zmienił.

---

## 1. Streszczenie (co da się zrobić i na jakiej podstawie)

W tej sesji przeprowadziliśmy pełny research (45 źródeł tekstowych, 6 gathererów, weryfikacja cytatów, self research) i **debatę multi-agent** nad wynikami. To dało zmierzone obserwacje, na których opieram propozycje:

| Obserwacja z tej sesji | Liczba / fakt | Skąd |
|---|---|---|
| Pierwsza tura gathererów (WebFetch) dała **fragmentaryczny** materiał | 5 z 6 odpowiedzi dla postów Brada zawierało tylko krótkie fragmenty (poniżej ok. 125 znaków na artykuł) | [`findings/bm-agents-fsm-findings.md`](../research/2026-10-14-agent-patterns-bradmurry/analysis/findings/bm-agents-fsm-findings.md) (luki G-1…G-5) |
| Naprawa: pełny tekst pozyskany inaczej | pełny tekst 43 źródeł pozyskany lokalnie (wklejki użytkownika albo `curl` + konwersja HTML); **kopie nie są w repozytorium**, źródła: [`SOURCES.md`](../research/2026-10-14-agent-patterns-bradmurry/SOURCES.md) | [`sources-index.md`](../research/2026-10-14-agent-patterns-bradmurry/analysis/sources-index.md) |
| Gatherzy **nie** sprawdzali własnych cytatów | trzech z sześciu napisało wprost, że nie porównali cytatów ze źródłem; „High" w ich findings znaczy „jawnie w tekście", nie „zweryfikowane" | raporty gathererów; [`01-cross-source-verification.md`](../research/2026-10-14-agent-patterns-bradmurry/analysis/01-cross-source-verification.md) |
| Skrypt zweryfikował cytaty w kilka minut | 1 448 cytatów, **1 373 zgodne dosłownie (94,8%)**, 75 niezgodnych (głównie artefakty detektora; nie przejrzałem wszystkich) | [`01-…` §5](../research/2026-10-14-agent-patterns-bradmurry/analysis/01-cross-source-verification.md) |
| Debata wykryła błędy **w mojej własnej syntezie** | z 12 twierdzeń sprawdzonych przez sędziego: opisanych 7; w tych 7: **5 błędnie przypisanych lub przesadzonych, 1 potwierdzone, 1 poprawione przez samego autora**. Jeden błąd (zgrupowanie trzech źródeł jako „niezależne potwierdzenie") pochodził z mojego dokumentu `01` i został poprawiony (erratum E1) | [`debate/judge-round1.md`](../research/2026-10-14-agent-patterns-bradmurry/debate/judge-round1.md), `01-…` §6 |
| Koszt debaty | ok. **0,52 mln tokenów** subagentów (3 stanowiska ≈ 198 tys., 3 krytyki ≈ 242 tys., sędzia ≈ 81 tys.; liczby z powiadomień o zakończeniu agentów, zawierają czytanie plików) | powiadomienia agentów w sesji (nie zapisane w plikach) |

**Wniosek:** największy realny zysk daje nie „więcej agentów", tylko **dwie rzeczy: (1) deterministyczne bramki sprawdzające to, co agenci twierdzą (cytaty, kompletność źródeł), oraz (2) krótki adwersaryjny przegląd syntezy przed uznaniem wniosków za gotowe.** Oba mają dowód z tej sesji. Pozostałe pomysły mają słabszy dowód i są niżej.

---

## 2. Co już robicie dobrze (nie ruszać bez powodu)
Z repo, nie z mojej pamięci (źródło: [`application-ideas.md`](../research/2026-10-14-agent-patterns-bradmurry/application-ideas.md) §B):
- GO gate z hookami, dwa punkty HITL, limity iteracji, 3-krotna weryfikacja (build + testy) jako **zewnętrzny sygnał**, pliki faz jako dziennik, RAG + graf wiedzy, routing modeli pod koszt.
- Walidacja przez niezależną sesję/model — według Waszych notatek znajduje realne błędy (m.in. regresję logowania, brakujące testy, błąd POST płatności gościa).
Research to potwierdza kierunkowo (Anthropic: „simplest solution first"; Huang i in.: samokorekta bez zewnętrznego sygnału zawodzi), ale **nie dowodzi, że to optimum**.

---

## 3. Gdzie zastosowanie wzorców realnie poprawi wyniki (propozycje)

### Gotowe do zrobienia (dowód z tej sesji jest bezpośredni)

| ID | Zmiana w workflow | Dlaczego (dowód) | Jak zmierzyć sukces | Kryterium „stop" (propozycja) |
|---|---|---|---|---|
| **W1** | **Bramka kompletności pozyskania źródeł** w `research-gather` (Faza 2, przed zbieraniem): zapisz metodę pozyskania, rozmiar wyciągu i status; jeśli odpowiedź jest fragmentem, nie wolno oznaczać źródła jako „pokrytego". Drabina: WebFetch → `curl` + ekstrakcja tekstu → prośba do użytkownika o wklejenie | 5 z 6 odpowiedzi było fragmentami, a wyszło to dopiero dzięki uczciwości gatherera | odsetek źródeł z pełnym tekstem przed zbieraniem | jeśli w 3 kolejnych researchach nie wykryje niczego → uprościć |
| **W2** | **Deterministyczny weryfikator cytatów** jako krok końca Fazy 2: skrypt porównuje cytaty z findings z plikami surowymi i raportuje odsetek zgodnych oraz listę niezgodnych. To odpowiednik „verify gate" Brada (recompute + diff) | 94,8% w minuty; gatherzy sami mówili, że nie weryfikowali. Skrypt wymaga poprawy: duża część 75 niezgodnych to artefakty detektora (fragmenty kodu, zmiana cudzysłowów), więc najpierw zmniejszyć fałszywe alarmy | odsetek zgodnych cytatów i liczba prawdziwych błędów na 100 cytatów | jeśli prawdziwych błędów < 1% w 3 researchach → skrypt zostaje, ale bez ręcznego przeglądu |
| **W3** | **Wymuszona kolumna „czy niezależne?"** w tabeli korelacji Fazy 3 (ta sama osoba/ten sam pipeline/ta sama baza = niezależne: nie). Reguła: zgodność kierunkowa ≠ potwierdzenie tego samego twierdzenia | mój błąd E1 w `01 §2` (trzy źródła o trzech różnych twierdzeniach potraktowane jako jedno potwierdzenie) | liczba twierdzeń oznaczonych „High", które po przeglądzie spadają | brak spadków w 3 researchach → zostawić jako szablon |

### Pilot (hipoteza, dowód częściowy)

| ID | Zmiana | Dowód i jego ograniczenia | Pomiar | Kryterium „stop" |
|---|---|---|---|---|
| **P1** | **Krok „red-team syntezy"** po Fazie 3 research-gather: debata/krytyka tylko nad spornymi lub ważnymi wnioskami, nie nad zbieraniem. Reguły z naszej debaty: różne perspektywy (zwolennik/sceptyk/pragmatyk), każdy debater sprawdza ≥ 4 źródła, sędzia sprawdza ≥ 5 twierdzeń, werdykt jako enum (`consensus`/`continue`), limit rund | Debata wykryła 5 przesadzonych/błędnych przypisań w 7 opisanych. **Ograniczenia:** debaterzy i sędzia to modele tej samej rodziny na jednej bazie dowodów, więc zgoda nie jest niezależnym potwierdzeniem; brak próby bez debaty, więc nie wiem, ile z tego wyłapałby jeden dobry przegląd | liczba **faktycznie skorygowanych** twierdzeń na 1 mln tokenów; porównanie z jednym krytykiem (`assumption-check`) na tym samym materiale | jeśli debata nie znajdzie nic, czego nie znalazł pojedynczy krytyk, w 2 researchach → zostaje tylko pojedynczy krytyk |
| **P2** | **Retrospektywny test na dawnych fazach**: weź 2–3 plany, po których niezależna walidacja znalazła realne błędy (np. fazy 7, 8, 9 z Waszych notatek), odtwórz plan, daj do krytyki przed implementacją i sprawdź, czy wychwyciłaby te błędy | tanie, bo nie buduje nic nowego i używa Waszych własnych danych | ile znanych błędów krytyka wykrywa przed implementacją | wychwytuje < połowę → nie dodawać kroku do pipeline |
| **P3** | **Debata dla spornych ADR** z realnymi alternatywami (przykład: odroczona decyzja o zakresie usunięcia martwego kodu ADR-0026) | wzorzec Debate/Consensus jest opisany jako „good for architecture decisions"; skuteczności w źródłach brak | czy debata ujawniła kwestię, której zwykły plan nie wykrył | nic nowego → nie powtarzać |
| **P4** | **Słownik wzorców jako tabela wyboru** „typ zadania → kształt workflow" w `agent-workflow.instructions.md` (łańcuch, routing, równoległość, orchestrator-workers, ewaluator-optymalizator, debata) na bazie taksonomii z researchu | Waszą routingową tabelę macie; wartość dodana niepewna | czy agenci rzadziej wybierają zły kształt (trzeba zdefiniować, jak to mierzyć) | brak mierzalnej różnicy → nie dodawać |

### Pomysły do przechowania (na razie bez dowodu z sesji)
Pełna lista z miarami: [`application-ideas.md`](../research/2026-10-14-agent-patterns-bradmurry/application-ideas.md) §B (B1–B8). Najważniejsze: zmierzyć rzeczywisty koszt kontekstu zamiast szacować (B1), odchudzić stały blok instrukcji metodą „jedna zmiana naraz" (B2), dziennik zdarzeń z hooka `PostToolUse` (B4), parytet agentów Claude Code z Copilot (B7), konsolidacja pamięci (B8). Pomysły dla aplikacji sklepu (A1–A6) są w tym samym pliku, część A.

---

## 4. Czego NIE rekomenduję
- Własnego runtime'u w stylu Aegis ani zbudowania frameworku multi-agent: jeden autor, brak publicznej weryfikacji; macie już HITL, limity i gate. ([`debate/decision.md`](../research/2026-10-14-agent-patterns-bradmurry/debate/decision.md) SKIP)
- Debaty jako stałego kroku każdej zmiany: koszt ≈ 0,5 mln tokenów na debatę, brak danych o skuteczności w źródłach.
- Automatycznych optymalizatorów promptów (GEPA/DSPy) przed istnieniem zestawu złotych zadań.

## 5. Ograniczenia i uczciwe zastrzeżenia
- **n = 1:** jedna sesja, jedna próba debaty, brak grupy kontrolnej. „Debata poprawiła wyniki" jest wykazane przez znalezione błędy, nie przez kontrolowane porównanie.
- **Wspólny bias:** debaterzy i sędzia to modele Claude; jedna baza dowodów, dominowana przez jednego autora (Brad). Sędzia sam to zapisał w `decision.md` §e.
- **Medium i Mastra:** luki (HTTP 403 / bramka e-mail) — nic z tych źródeł nie wpłynęło na wnioski.
- **Liczby tokenów** debaty pochodzą z powiadomień o zakończeniu agentów w sesji; nie są zapisane w żadnym pliku repo.
- **Weryfikacja cytatów:** 94,8% zgodnych; 75 niezgodnych nie przejrzałem pojedynczo.
- Część materiału (13 artykułów per wzorzec, 8 części serii, 7 stron AutoGen) to **rozszerzenie zakresu** poza pierwotne 18 URL; oznaczone w [`sources-index.md`](../research/2026-10-14-agent-patterns-bradmurry/analysis/sources-index.md).

---

## 6. Referencje i proweniencja (jak prześledzić, skąd to się wzięło)

### 6.1 Oś czasu (2026-10-14)
1. Prośba: raport z linków (blog bradmurry.com, Medium, Mastra, AutoGen) + debata multi-agent o zastosowaniach w AI.
2. **Faza 1** (brief): [`planning/research-brief.md`](../research/2026-10-14-agent-patterns-bradmurry/planning/research-brief.md); typ external, ścieżka wyjściowa zatwierdzona przez Ciebie.
3. **Faza 2A** (plan): [`planning/research-plan.md`](../research/2026-10-14-agent-patterns-bradmurry/planning/research-plan.md), [`planning/sources.md`](../research/2026-10-14-agent-patterns-bradmurry/planning/sources.md).
4. **Faza 2B**, tura 1 (WebFetch, fragmentaryczna): 5 plików findings w [`analysis/findings/`](../research/2026-10-14-agent-patterns-bradmurry/analysis/findings/).
5. Uzupełnienie pełnym tekstem (wklejki + `curl`; 43 źródła, kopie tylko lokalnie, adresy w [`SOURCES.md`](../research/2026-10-14-agent-patterns-bradmurry/SOURCES.md).
6. **Faza 2B**, tura 2 (z pełnego tekstu): 6 plików `*-raw-*findings.md`.
7. **Faza 3**: [`00-summary.md`](../research/2026-10-14-agent-patterns-bradmurry/analysis/00-summary.md), [`01-cross-source-verification.md`](../research/2026-10-14-agent-patterns-bradmurry/analysis/01-cross-source-verification.md) (z erratum), [`02-rejected-information.md`](../research/2026-10-14-agent-patterns-bradmurry/analysis/02-rejected-information.md).
8. **Self research** (dodatkowy): [`../research/2026-10-14-llm-self-research/README.md`](../research/2026-10-14-llm-self-research/README.md).
9. **Debata**: [`positions/`](../research/2026-10-14-agent-patterns-bradmurry/debate/positions/), [`critiques/`](../research/2026-10-14-agent-patterns-bradmurry/debate/critiques/), [`judge-round1.md`](../research/2026-10-14-agent-patterns-bradmurry/debate/judge-round1.md), [`decision.md`](../research/2026-10-14-agent-patterns-bradmurry/debate/decision.md).
10. Pomysły: [`application-ideas.md`](../research/2026-10-14-agent-patterns-bradmurry/application-ideas.md). Ten raport: wynik końcowy.

### 6.2 Które źródło stoi za którym twierdzeniem
| Temat w tym raporcie | Źródła pierwotne (URL w `sources-index.md` / `self-research` §13) | Plik surowy / findings |
|---|---|---|
| Deterministyczna powłoka, reducer, parkowanie, guardy | bradmurry.com: binding-llms-to-FSM, walking-HIL-FSM, reactive-reducer-patterns (+intro) | [binding-llms-to-FSM](https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/), [walking-HIL-FSM](https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/), [reactive-reducer-patterns](https://bradmurry.com/software/reactive-reducer-patterns/), [intro](https://bradmurry.com/software/reactive-reducer-patterns-intro/); [`bm-agentic-core-raw-findings.md`](../research/2026-10-14-agent-patterns-bradmurry/analysis/findings/bm-agentic-core-raw-findings.md) |
| 13 wzorców (w tym Debate/Consensus) | bradmurry.com: 13 artykułów per wzorzec (adresy: [`SOURCES.md` §B1](../research/2026-10-14-agent-patterns-bradmurry/SOURCES.md)) | [lista wzorców](https://bradmurry.com/software/reactive-reducer-patterns/); `bm-patterns-1-7-raw-…`, `bm-patterns-8-13-raw-…` |
| „Verify gate", trzy sygnały, „clean journal ≠ good outcome" | bradmurry.com: self-evolving agents (intro + części 1–4) | [self-evolving-agents-intro](https://bradmurry.com/software/self-evolving-agents-intro/), części 1–4: [`SOURCES.md` §B2](../research/2026-10-14-agent-patterns-bradmurry/SOURCES.md); [`bm-series-parts-raw-findings.md`](../research/2026-10-14-agent-patterns-bradmurry/analysis/findings/bm-series-parts-raw-findings.md) |
| Idempotencja, journal, replay, operowanie w produkcji | bradmurry.com: Agents in Production (intro + części 1–4) | [agents-in-production-intro](https://bradmurry.com/software/agents-in-production-intro/), części 1–4: [`SOURCES.md` §B2](../research/2026-10-14-agent-patterns-bradmurry/SOURCES.md) |
| Debata (AutoGen): solverzy + głosowanie, rozbieżności proza/kod | microsoft.github.io/autogen … design-patterns (9 stron) | 9 stron: [`SOURCES.md` §A2 i §B3](../research/2026-10-14-agent-patterns-bradmurry/SOURCES.md); [`autogen-raw-9pages-findings.md`](../research/2026-10-14-agent-patterns-bradmurry/analysis/findings/autogen-raw-9pages-findings.md) |
| Zasada „najprostsze najpierw", workflow vs agent, routing, evaluator-optimizer | Anthropic, *Building Effective Agents* | [`llm-self-research` §1, §5, §8](../research/2026-10-14-llm-self-research/README.md) |
| Biasy sędziego LLM, samokorekta bez zewnętrznego sygnału | Zheng i in. 2023 (arXiv 2306.05685); Huang i in. 2023 (arXiv 2310.01798) | `llm-self-research` §2, §3 |
| Prompt caching (ceny, TTL, unieważnianie) | dokumentacja Anthropic i OpenAI (stan 2026-10-14) | `llm-self-research` §6 |
| Wasz obecny setup (GO gate, pipeline, koszty kontekstu) | pliki repo: `.claude/settings.json`, `tools/hooks/README.md`, `.github/AGENT-PIPELINE.md`, `.github/context-cost-analysis.md` | [`application-ideas.md` §B](../research/2026-10-14-agent-patterns-bradmurry/application-ideas.md) |
| Wasze notatki o walidacji niezależnej sesji | pamięć asystenta (poza repo), np. fazy 7, 8, 9 gościnnego checkoutu | **nie ma w repo**: do uzupełnienia z `.github/plans` przy P2 |

### 6.3 Indeks i nawigacja
- Pełny indeks proweniencji źródeł (generowany z nagłówków plików): [`sources-index.md`](../research/2026-10-14-agent-patterns-bradmurry/analysis/sources-index.md).
- Wejście do całego researchu: [`README.md`](../research/2026-10-14-agent-patterns-bradmurry/README.md).
- Poprawki po debacie: `01-cross-source-verification.md` §6 (erratum E1, E2).
- **Uwaga:** `docs/README.md` nie wymienia folderu `docs/research/`. Dodanie go do indeksu wymaga edycji poza ścieżkami zawsze dozwolonymi, więc czeka na `IMPLEMENT`.

> **Zobacz też:** [13 wzorców vs nasz setup: kompromisy i guardrails G1–G11](2026-10-14-agent-patterns-fit-tradeoffs-guardrails.md) (uzupełnienie z tego samego dnia, zawiera uwagi użytkownika).
