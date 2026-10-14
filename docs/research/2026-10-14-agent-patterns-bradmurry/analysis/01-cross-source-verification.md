# Cross-Source Verification (Phase 3, step 2)

Data: 2026-10-14. Konwencja pewności: methodology §8 (High = ≥2 niezależne źródła/jawne dowody; Medium = jedno źródło; Low = spekulacja/sprzeczne). **Uwaga:** w plikach `*-raw-findings.md` „High" oznacza „twierdzenie jest jawnie w tekście", a **nie** „zweryfikowane niezależnie". W tym dokumencie stosuję ścisłą skalę.

## 1. Sprzeczności i niespójności (nie rozstrzygam wyboru jednej strony)

| # | Źródło A mówi | Źródło B mówi | Charakter |
|---|---|---|---|
| C1 | `08` (intro serii): każda pętla jest ograniczona „by a verdict enum, policy.maxIterations, and the reducer's no-progress guard" | `13`, `14`, `16` (autor sam się poprawia): no-progress guard „does not span turns", zabezpieczenia to enum + `maxIterations`, „two rather than three" | **Autokorekta autora** — wcześniejsze deklaracje osłabione. Intro serii nie poprawione. |
| C2 | `09` (pipeline): `implemented` „NOT a bool" (bool resetowany do `false` zostaje „present") | `09`, tabela końcowa: „implemented = true" | Wewnętrzna niespójność, niewyjaśniona przez autora |
| C3 | `26` (AiP Part 1): YAML incident-triage ma `derive-key`, `notifyKey`, `attempts`, re-entrant `notify` | `27`: ta sama nazwa FSM, `notify` wymaga `notifyRef` z `opsTool`, bez `derive-key` | Dwie różne wersje „tego samego" workflow |
| C4 | `23` (data quality): tabela sygnałów ma 3 fakty + 1 opinię | tekst: „Two facts, one opinion" | Błąd redakcyjny (oznaczony `[sic]`) |
| C5 | `23`: tokeny po Round 2 = 38 000 | optimum = 41 000 (wyższe niż po Round 2, choć nazwane „optimum") | Metryka rośnie w „optimum" — niewyjaśnione |
| C6 | `25` (entity resolution): finalny FSM bez bloku `policy` | tekst serii wszędzie podkreśla `maxIterations`/policy jako zabezpieczenie | Niespójność przykład–zasada; podział strojenia na rundy w podsumowaniu też nie zgadza się z opisem rund |
| C7 | AutoGen debate (`39`): proza „agent A is connected to agents B and C" | rysunek i `TypeSubscription`: A sąsiaduje z B i D | Proza vs kod (oba zacytowane) |
| C8 | AutoGen debate: proza „SolverRequest" rozpoczyna debatę | kod publikuje `Question` | Proza vs kod |
| C9 | AutoGen reflection (`40`): proza wymienia „max iterations" jako warunek stopu | kod nie ma limitu iteracji | Proza vs kod — możliwa pętla nieskończona |
| C10 | AutoGen group chat (`36`): proza „APPORVED" od edytora | kod sprawdza tylko wiadomość użytkownika „approve" | Proza vs kod; strona sama: „not meant to be used in real applications" |
| C11 | AutoGen mixture-of-agents (`38`): 3 warstwy workerów, orkiestrator syntetyzuje, wynik „published" | `range(num_layers-1)` → 2 warstwy; syntezę robią workerzy; wynik zwracany bezpośrednio | Proza vs kod |
| C12 | AutoGen handoffs (`37`): „Refund Agent" | kod: `IssuesAndRepairsAgent` | Nazwa w prozie ≠ kod |
| C13 | `32` (config-driven-transforms): nagłówek „schleppy transforms" | import `plot.transform.processor` | Nazwa narzędzia niespójna |
| C14 | użytkownik: „14 postów" | indeks bloga: 13 postów + strona indeksu = 14 URL-i | Nie sprzeczność źródeł — doprecyzowanie liczby |

## 2. Korelacja między źródłami (co jest potwierdzone niezależnie, a co nie)

| Twierdzenie | Źródła | Niezależne? | Ocena |
|---|---|---|---|
| „Sędzia/krytyk LLM to słaby sygnał; potrzebny zewnętrzny, deterministyczny check" | Brad (`07`, `22`–`25`: „the weakest signal… drifts toward 'looks thorough'") ⟷ Zheng i in. 2023 (biasy position/verbosity/self-enhancement) ⟷ Huang i in. 2023 („struggle to self-correct… without external feedback") | trzy niezależne grupy, ale **różne twierdzenia** (patrz erratum E1 na końcu pliku) | **Medium** (skorygowane po debacie; pierwotnie „High kierunkowo") |
| „Pętla musi mieć jawny limit/stop poza modelem" | Brad (maxIterations, cap 128) ⟷ Anthropic („Stopping conditions, such as a maximum iteration count") ⟷ blogi o ReAct | częściowo | **High** (kierunkowo) |
| „Zacznij od najprostszego; autonomia = koszt i błędy kumulacyjne" | Anthropic ⟷ Brad („Not every workflow needs a full FSM… a for-loop could do for free", `29`) | tak | **High** |
| „Różnorodność modeli, nie inna pętla, pomaga na wspólne ślepe punkty (debata/quorum)" | Brad (`14`, `20`) ⟷ literatura o biasach sędziów (self-enhancement) | częściowo; literatura nie testuje debaty u Brada | **Medium** |
| „Wzorzec reactive reducer daje determinizm, replay, ograniczony koszt" | tylko Brad (kilka postów) | **nie** — jeden autor, kilka postów to nie niezależne potwierdzenie | **Medium** (twierdzenie autora) |
| Metryki baseline→optimum (4 przykłady) | tylko Brad | nie; bez prób/powtórzeń | **Low–Medium** |
| „Debata wielu agentów poprawia wynik" | AutoGen: tylko opis + jeden przykład z pełną zgodnością; Brad: brak liczb | nie ma danych w źródłach | **Low** (brak dowodu skuteczności w zebranym materiale) |
| Zewnętrzne dowody skuteczności multi-agent debate (np. paper o sparse topology wymieniony na stronie AutoGen) | strona AutoGen linkuje arXiv 2406.11776 — nie pobrane | — | **luka** |

## 3. Luki (Missing Information)
- Medium (403), Mastra (treść rozdziałów).
- Brak jakiegokolwiek niezależnego benchmarku dla runtime'u „Aegis"; brak publicznego repo w materiale.
- Brak danych o skuteczności debaty/quorum w źródłach (Brad: opisy mechaniki, autor sam: „can't tell you the consensus is right").
- AiP części 5–7 oraz akty II–III; 13 podstron wzorców były dostępne tylko dzięki wklejeniu przez użytkownika.
- Zatrzymano się na „stanie z 2026-10-14"; daty publikacji większości postów Brada nie są znane (część stron ma „Jul 2026", „Sep 2026" w wyciągu).

## 4. Declarative Conclusions (consolidated)
Nie dotyczy — źródła zewnętrzne (publikacje), nie transkrypty z mówcami. Potraktowane jako *twierdzenia autora*: oznaczone w findings jako „single-source / self-reported". Najbardziej ryzykowne dla późniejszych wniosków (filtr wpływu analitycznego, methodology §9):

| # | Twierdzenie | Autor | Uzasadnienie w źródle | Korroboracja | Werdykt |
|---|---|---|---|---|---|
| D1 | „the glue code is gone" | Brad (`03`) | tylko deklaracja | brak | **Unsupported** |
| D2 | „This series is all true. Not a thought experiment" (`07`) | Brad | archiwum 5 wersji disease-etl | pliki archiwum niedostępne | **Partially supported** |
| D3 | Wynik 2020 „billions of requests… didn't even break a sweat" (`30`) | Brad | brak liczb poza tym | brak | **Unsupported** (samoopis) |
| D4 | „90% fast track / 80% of use case" (`43`) | Brad | brak | brak | **Unsupported** |
| D5 | „An LLM is a fantastic contributor. It is a terrible state machine." (`03`) | Brad | uzasadnienie narracyjne | zgodne z Huang i in. na poziomie kierunku (nie dosłownie) | **Partially supported** |
| D6 | SLM „sufficiently powerful, inherently more suitable, necessarily more economical" (arXiv 2506.02153) | Belcak i in. | position paper | brak niezależnych w zebranych materiałach | **Partially supported** (autorzy sami nazywają to „value statement") |

## 5. Weryfikacja cytatów (automatyczna, 2026-10-14)
Skrypt porównał cytaty w cudzysłowach (≥25 znaków) z plików `*-raw-*findings.md` z tekstem plików `sources-raw/` (po normalizacji białych znaków, apostrofów, backticków). Wynik:

| Plik findings | Cytaty | Zgodne | Niezgodne |
|---|---|---|---|
| autogen-raw-9pages | 173 | 160 | 13 |
| bm-agentic-core-raw | 177 | 173 | 4 |
| bm-infra-raw | 294 | 285 | 9 |
| bm-patterns-1-7-raw | 232 | 212 | 20 |
| bm-patterns-8-13-raw | 279 | 273 | 6 |
| bm-series-parts-raw | 293 | 270 | 23 |
| **Razem** | **1 448** | **1 373 (94,8%)** | **75 (5,2%)** |

Ręczny przegląd próbki niezgodnych: większość to **artefakty detektora** (fragmenty kodu w backtickach, pary cudzysłowów obejmujące zdania zamiast cytatów, wewnętrzne cudzysłowy podwójne zamienione na pojedyncze przy cytowaniu, np. `'how many have answered'`) — nie zmyślone cytaty. **Nie przejrzałem wszystkich 75 pojedynczo**; traktuj 94,8% jako dolną granicę zgodności dosłownej, a pozostałe jako „do ręcznego sprawdzenia przed użyciem w publikacji".

## 6. Erratum (po debacie, 2026-10-14)
- **E1 (wiersz „Sędzia/krytyk LLM to słaby sygnał…" w §2):** pierwotnie zgrupowałem trzy źródła jako potwierdzające jedno twierdzenie i dałem „High". Sędzia debaty (`../debate/judge-round1.md`) uznał to za przesadę. Prawidłowo: Zheng i in. 2023 pokazują **biasy sędziego** (position, verbosity, self-enhancement) i >80% zgodności z ludźmi, czyli sędzia wymaga kalibracji, a nie „jest słaby"; Huang i in. 2023 dotyczą **samokorekty bez zewnętrznego feedbacku**; Brad twierdzi, że krytyk LLM to „weakest signal". To trzy zbliżone, ale różne twierdzenia, więc zgodność jest tylko kierunkowa (**Medium**). Błąd był w mojej syntezie, nie w źródłach.
- **E2:** „Low" w §2 dotyczy wyłącznie skuteczności **debaty**. Nie obejmuje wzorców 10 (aukcja) i 13 (dynamic team), dla których w materiale nie ma żadnej oceny skuteczności (brak danych, nie dowód przeciw).
