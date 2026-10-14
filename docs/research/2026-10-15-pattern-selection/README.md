# Dobór wzorca agentowego: czy da się go wyliczyć, i sprawdzenie hipotez niepopartych / niesprawdzonych

- **Data:** 2026-10-15 (źródła pobrane tego dnia; daty publikacji przy każdym źródle)
- **Tryb:** samodzielny research asystenta (WebSearch + WebFetch + skrypty lokalne). Nie przeszedł niezależnej weryfikacji krzyżowej.
- **Powiązane:** [raport o przeglądzie i hipotezach H1–H9](../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md) · [dopasowanie wzorców i guardrails](../../reports/2026-10-14-agent-patterns-fit-tradeoffs-guardrails.md) · [główny research](../2026-10-14-agent-patterns-bradmurry/README.md) · [self research](../2026-10-14-llm-self-research/README.md)
- **Pliki w tym folderze:** `scripts/select_pattern.py` (symulacja), `results.md` (jej wynik), `pattern-decision-card.draft.md` (szkic karty wzorców, tylko artefakt badawczy).

**Pytanie użytkownika:** czy da się dynamicznie dobrać właściwy wzorzec (debata, planner/executor itd.) do problemu i czy da się to „wyliczyć"? Do tego: sprawdzić hipotezy, które w poprzednim przeglądzie wyszły jako niepoparte lub niesprawdzone.

---

## 1. Krótka odpowiedź

**Tak, częściowo.** Da się to **policzyć jawną regułą** z kilku cech zadania; da się ją przetestować pod kątem spójności i odporności. **Nie da się dziś wykazać jej trafności**, bo nie mamy etykiet „który wzorzec był właściwy". Literatura zna dwa poziomy: (a) **jakościowe** modele decyzyjne (opisy „kiedy co"), (b) **wyuczone predyktory** na danych z benchmarków. Nasza reguła to poziom pośredni: przejrzysta reguła z cech, do skalibrowania w trybie cienia. Dwóch rzeczy z git nie obliczysz: **czy podzadania są naprawdę niezależne** i **czy wniosek jest sporny**. To musi oszacować agent albo człowiek.

---

## 2. Co mówi literatura (stan na 2026-10-15)

Poziom pewności: **Wysoka** = pierwotne źródło pobrane, **Średnia** = abstrakt lub jedno źródło, **Niska** = fragment z wyszukiwarki.

| Źródło (data) | Co mówi o doborze | Pewność | Zastrzeżenia |
|---|---|---|---|
| **Anthropic, *How we built our multi-agent research system*** ([link](https://www.anthropic.com/engineering/multi-agent-research-system); data publikacji niepodana w wyciągu) | **Jedyna znaleziona jawna reguła skalowania wysiłku:** proste ustalenie faktów: 1 agent, 3–10 wywołań narzędzi; porównania: 2–4 subagentów po 10–15 wywołań; złożone badanie: > 10 subagentów z jasno podzieloną odpowiedzialnością. Koszt: agenci ≈ 4× tokenów względem czatu, multi-agent ≈ 15×. **Słabe dopasowanie:** zadania wymagające wspólnego kontekstu u wszystkich agentów, z wieloma zależnościami, oraz kodowanie („fewer truly parallelizable tasks than research"). Obserwowane błędy: 50 subagentów dla prostych zapytań, zdublowana praca przy niejasnym zleceniu | Średnia–wysoka | źródło producenta; reguła jest **w promptach**, nie obliczana |
| **Kim i in., *Towards a Science of Scaling Agent Systems*** ([arXiv 2512.08296](https://arxiv.org/abs/2512.08296), v1 2025-12-09, v3 2026-04-08; [wpis Google Research](https://research.google/blog/towards-a-science-of-scaling-agent-systems-when-and-why-agent-systems-work/)) | Model predykcyjny dobiera najlepszą architekturę dla **87%** konfiguracji spoza próby uczącej. Koordynacja pomaga przy zadaniach podzielnych (do ok. +80%), **szkodzi przy sekwencyjnych** (do −70%, wg wpisu: „39–70%"), zadania z wieloma narzędziami mają narzut. Architektury bez centralnej weryfikacji **wzmacniają błędy** (niezależni: 17,2×; scentralizowany: 4,4×). Wejścia predyktora: liczba narzędzi, podzielność zadania, zależności sekwencyjne | Średnia | **Rozbieżność wersji:** abstrakt v3: 260 konfiguracji, 6 benchmarków, R² (CV) 0,373; wpis: 180 konfiguracji, 4 benchmarki, R² 0,513. Nie rozstrzygnąłem, która wersja jest właściwa. Dopasowanie skromne, benchmarki nie są naszą domeną |
| **Zhang i in., *Stop Overvaluing Multi-Agent Debate…*** ([arXiv 2502.08788](https://arxiv.org/abs/2502.08788), v1 2025-02-12, v3 2025-06-21; wcześniejszy tytuł: *If Multi-Agent Debate is the Answer, What is the Question?*) | 5 metod debaty, 9 benchmarków, 4 modele: debata **często nie przewyższa** prostych baz (Chain-of-Thought, Self-Consistency), mimo większego kosztu obliczeń. **Heterogeniczność modeli** jako „universal antidote" | Średnia (abstrakt) | benchmarki rozumowania i QA, nie ocena syntezy źródeł |
| **Huang i in.** ([arXiv 2310.01798](https://arxiv.org/abs/2310.01798), 2023-10-03) | wg fragmentów z wyszukiwarki: na GSM8K debata ≈ self-consistency przy 3 odpowiedziach (83,2 vs 82,5), a przy 9 odpowiedziach wypada gorzej (83,0 vs 88,2) | **Niska** | liczby z fragmentów wyszukiwarki, nie z samego artykułu |
| **Yue i in., *MasRouter*** ([ACL 2025](https://aclanthology.org/2025.acl-long.757), [arXiv 2502.11133](https://www.arxiv.org/pdf/2502.11133)) | wyuczony kontroler wybiera po kolei tryb współpracy, role i model; deklaracja autorów: do 52,07% niższy narzut na HumanEval | Niska–średnia | deklaracje autorów, nie weryfikowane niezależnie; wymaga danych treningowych |
| **Liu i in., katalog wzorców** ([arXiv 2405.10467](https://arxiv.org/abs/2405.10467), v4 2024-11-06, §5.1) | **Model decyzyjny jest jakościowy:** np. incremental querying, gdy chcemy większej pewności rozumowania; one-shot, gdy budżet wywołań jest ograniczony; single-path vs multi-path plan; self/cross/human reflection; wieloagentowa refleksja przez głosowanie, role lub debatę. **Nie omawia kosztu tokenów ani zbieżności** dla debaty i głosowania | Średnia | z pobranego HTML v4 |

**Wniosek z literatury:** dla debaty dowody **nie są po jej stronie jako domyślnego wyboru** (Zhang), a jedyny realnie testowany **predyktor** doboru (Kim) wykorzystuje cechy, które da się częściowo ocenić: podzielność, zależności sekwencyjne, liczbę narzędzi. Nasz własny wynik z 2026-10-14 (5 z 7 opisanych twierdzeń syntezy było błędnie przypisanych lub przesadzonych) dotyczy **krytyki syntezy ze sprawdzaniem źródeł**, a nie zadań, które testują te prace; nie przenoszę go automatycznie.

---

## 3. Trzy poziomy „wyliczania"

| Poziom | Opis | Wymaga | Dziś |
|---|---|---|---|
| (a) Jakościowy | drzewo pytań „kiedy co" (Liu §5.1, Anthropic) | wiedzy eksperckiej | jest w literaturze i w naszych raportach |
| (b) Reguła z cech | jawny wynik/priorytet z kilku oszacowanych cech, z progami | oszacowania cech | **zrobione** w `scripts/select_pattern.py`, przetestowane na git |
| (c) Wyuczony predyktor | model trenowany na etykietach (Kim, MasRouter) | danych: zadanie → najlepszy wzorzec i wynik | **brak danych** |

---

## 4. Proponowana reguła doboru (poziom b)

Cechy zadania (do oszacowania przez agenta lub człowieka; w symulacji przybliżane z git): **rozmiar** (S/M/L), **niezależność podzadań**, **niepewność ścieżki** (kroki nieznane z góry), **dostępność deterministycznego checku**, **ryzyko / odwracalność**, **czy wniosek jest sporny**, **gęstość narzędzi i zależności**.

Kolejność decyzji (pierwsza pasująca gałąź wygrywa, wzorce się nakładają):

| # | Warunek | Wzorzec | Poziom autonomii |
|---|---|---|---|
| 1 | mały rozmiar i niskie ryzyko | **brak wzorca** | – |
| 2 | wszystko pozostałe | **sekwencja z bramkami** (domyślna) | A |
| 3 | kroki nieznane z góry, może być potrzebne ponowne planowanie | **Planner → Executor**; po 2 re-planach pytaj | B |
| 4 | duże zadanie, ≥ 3 niezależne obszary, mały wspólny stan | **fan-out tylko do odczytu + kontrola podziału** (nie dla edycji na wspólnych plikach: Kim, Anthropic) | A |
| 5 | wysokie ryzyko | najpierw checki deterministyczne, potem **niezależna walidacja** (inna sesja/model); jeśli nie ma checku, dodać go najpierw | B |
| 6 | wniosek sporny i wpływa na kosztowną decyzję | **jeden krytyk ze sprawdzaniem źródeł** | A |
| 7 | ≥ 2 realne alternatywy, brak checku deterministycznego, kosztowny błąd | **debata** (3 perspektywy + sędzia, enum werdyktu, ≤ 2 rundy, **różne modele, jeśli dostępne**) | C: zapytaj najpierw |
| 8 | binarna decyzja o wysokiej stawce | **quorum 3 niezależnych głosów** | C |

**Podreguła „debata czy nie":** domyślnie jeden krytyk (poz. 6). Debata dopiero, gdy **wszystkie** warunki z poz. 7 są spełnione; zadania sekwencyjne i gęste w narzędzia traktować jako niekorzystne dla koordynacji (Kim).
**Podreguła „planner/executor czy nie":** gdy specyfikacja jest jasna i kroki znane, pomijamy plan-wykonanie i robimy sekwencję; gdy kroki zależą od odkryć w trakcie (Anthropic: orchestrator-workers dla nieprzewidywalnych podzadań), dobieramy poz. 3.

**Czego ta reguła nie liczy:** niezależności podzadań (potrzebuje oceny agenta albo człowieka) i spornych wniosków. To właśnie dlatego traktuję ją jako **doradczą**, nie decydującą.

---

## 5. Symulacja na historii git (200 commitów, 2026-05-27 – 2026-10-14)

Pełne wyniki: [`results.md`](results.md). Wersja ostrzegawcza: commit to nie zadanie, cechy to proxy z git, więc **testuję spójność i odporność, nie trafność**.

| Wynik | Wartość |
|---|---|
| Brak wzorca (mała zmiana, niskie ryzyko) | 34% commitów |
| Test brzegowy: małe zadania (S) z rekomendowanym wzorcem | **0 z 21** |
| Średnia zgodność rekomendacji przy losowej zmianie progów (300 kombinacji) | **90%**; 70% commitów stabilnych (≥ 90%), 6% niestabilnych (< 60%) |
| Zmiana poziomu autonomii przy zmianie progów | średnio 5% kombinacji |

**Kalibracja pokazała istotną rzecz.** W wariancie bazowym niezależna walidacja wypada dla **42%** commitów, bo „≥ 3 projekty" liczyło `docs/` i projekty testowe jako osobne projekty (81 commitów). Po **jednej zmianie** (liczyć tylko projekty źródłowe, bez docs/testów/.github) wypada dla **20%**:

| Poziom autonomii | Baza | Wariant 2 |
|---|---|---|
| brak wzorca | 34% | 34% |
| A | 23% | 44% |
| B (ogłoś i działaj) | 38% | 20% |
| C (zapytaj) | 5% | 4% |

**Co z tego wynika (hipotezy):**
1. Reguła jest **stabilna**, ale **stabilna ≠ trafna**: nadmierne uruchamianie walidacji wynikało z jednej niedokładnej cechy, nie z progów. Jakość doboru zależy głównie od **definicji cech**, a nie od liczb w progach.
2. Przy wariancie 2 pytań do użytkownika (poziom C) byłoby ok. **4%** zadań; ogłoszeń (B) ok. **20%**. To da się sprawdzić na prawdziwym ruchu w trybie cienia.
3. Prawdziwą trafność da się ocenić dopiero na etykietach: **arkusz 20 commitów** do oznaczenia jest w `results.md` §3 (kolumna „Zgoda?" pusta).

---

## 6. Sprawdzenie hipotez niepopartych / niesprawdzonych (H1–H9)

Pierwotne definicje i warunki PASS: [raport o przeglądzie §6](../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md). Poniżej **zaktualizowany stan po dzisiejszych sprawdzeniach**.

| ID | Było | Co sprawdziłem | Jest teraz |
|---|---|---|---|
| **H1** dobór z kontekstu ma sens, bo zadania są M/L | częściowo | symulacja: 66% commitów dostaje co najmniej sekwencję z bramkami, a 34% brak wzorca; M/L ≈ 90% (S1) | **częściowo poparte**; brakuje oznaczeń człowieka (arkusz gotowy) |
| **H2** deterministyczne checki reguł złapią coś realnego | niepoparte | rozszerzony skan (11 sprawdzeń); naruszenia w 2 grupach: **8 linii rejestracji usług w `Startup.cs`** (reguła w `dotnet.instructions.md:169` nie ma wyjątku dla Web/API, a Web ma `WebExtensions.cs`) i **bezpośrednie `fetch(...)` w 2 plikach JS** (`cartNotification.js`, `checkout-placeorder.js`; reguła: używać helpera `ajaxRequest`). `User.Claims` tylko w `BaseController` (dozwolone) | **częściowo poparte, skala mała** (ok. 10 linii/2 pliki); nie wiadomo, czy to tolerowane wyjątki |
| **H3** bramki dowodowe w researchu | poparte | bez zmian | **poparte** |
| **H4** debata ponad jednego krytyka | niesprawdzone | literatura: Zhang i in. (debata często nie przewyższa prostych baz; pomaga heterogeniczność modeli) | **niepoparte jako domyślny wybór**; zostaje poziom C, wymóg heterogeniczności; nasz wynik dotyczy krytyki syntezy, nie benchmarków |
| **H5** trafny dobór przez agenta | niesprawdzone | reguła policzona i przetestowana na odporność; nie testowałem decyzji LLM | **nadal niesprawdzone**; przygotowany arkusz trybu cienia (reguła, nie agent) |
| **H6** karta wzorców nie zjada tokenów | niesprawdzone | **zmierzyłem szkic karty**: 1 821 znaków, 321 słów, **≈ 455 tokenów** (zakres 387–524 wg heurystyki z `context-cost-analysis.md`, ±15%) = ok. **6,4%** stałego bloku 7 110 tokenów, gdyby była ładowana zawsze | **zmierzone na szkicu**; uzasadnia ładowanie na żądanie (dla zadań S zero), nie ładowanie stałe |
| **H7** czatbot tylko do odczytu jest wykonalny | częściowo | w warstwie Application istnieje **dostęp oparty o token bez ClaimsPrincipal**: `IOrderAccessService.GetScopeAsync(token)` i `HasAccessAsync(orderId, token)` | **lepiej poparte** (backend może sprawdzić dostęp bez cookie); end-to-end niesprawdzone |
| **H8** spójność Copilot i Claude Code | niesprawdzone | nie da się bez uruchomienia obu narzędzi | **niesprawdzone** |
| **H9** brak regresji jakości i kosztu | niesprawdzone | wymaga zestawu zadań referencyjnych | **niesprawdzone** |

**Nadal niesprawdzone (wymaga Twojego udziału lub realnego uruchomienia):** H5 w wersji z LLM, H8, H9, ocena arkusza H1/H5.

---

## 7. Co się zmienia w raportach po tym sprawdzeniu
1. **Debata:** zostaje na poziomie C (na prośbę), z wymogiem różnych modeli; nie ma dowodów, że jest lepsza niż jeden krytyk ze sprawdzaniem źródeł.
2. **Fan-out:** tylko do odczytu i dla naprawdę niezależnych pytań; unikać przy edycjach na wspólnych plikach i przy zadaniach sekwencyjnych (dowód: Kim, Anthropic).
3. **Reguła skalowania wysiłku** (Anthropic: 1 agent / 2–4 subagentów / > 10) to **jedyna znaleziona jawna reguła liczbowa**; traktujemy ją jako punkt odniesienia, nie jako naszą.
4. **Koszt kotwica:** multi-agent ≈ 15× tokenów względem czatu (Anthropic); nasza debata ≈ 0,52 mln tokenów subagentów.
5. **H2 częściowo odzyskuje sens:** wykryto 2 grupy realnych odstępstw od reguł; skala mała.
6. **H6 zmierzone na szkicu:** ≈ 455 tokenów.

## 8. Ograniczenia
- Wszystkie źródła naukowe przeczytane wyłącznie przez abstrakt lub streszczenie narzędzia; liczby z wyszukiwarki oznaczone jako **Niska**.
- Kim i in.: rozbieżne wersje (liczba konfiguracji, R², wartości zysku/straty).
- Symulacja na git testuje spójność reguły, nie trafność; commit ≠ zadanie.
- Regexy skanu reguł są przybliżeniami; nie rozdzieliłem kodu legacy od nowego.
- Pomiar karty to heurystyka znaków/4 na szkicu, nie rzeczywisty tokenizer ani rzeczywiste ładowanie w narzędziu.
- Nie zrobiłem testu LLM doboru (H5), testu A/B debaty (H4) ani testu w Copilocie (H8).
