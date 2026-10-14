# 13 wzorców agentowych: dopasowanie do naszego setupu, kompromisy i guardrails

- **Data:** 2026-10-14
- **Status:** zapis rozmowy i analizy, żeby nic nie zginęło. **To nie jest plan wdrożenia** (celowo: bez kroków implementacji). Oceny to hipotezy poparte plikami śledzonymi w repo i wynikami researchu.
- **Autorstwo:** pomysły i uwagi do poszczególnych wzorców pochodzą od użytkownika (oznaczone „Uwaga użytkownika"). Analiza dopasowania, kompromisy i guardrails: asystent.
- **Powiązane raporty i źródła:** [raport końcowy o ulepszeniu workflow](2026-10-14-agent-patterns-workflow-improvements.md) · [katalog 13 wzorców](../research/2026-10-14-agent-patterns-bradmurry/analysis/00-summary.md) · [decyzja debaty](../research/2026-10-14-agent-patterns-bradmurry/debate/decision.md) · [indeks źródeł](../research/2026-10-14-agent-patterns-bradmurry/analysis/sources-index.md) · [self research](../research/2026-10-14-llm-self-research/README.md)
- **Sprawdzone w repo (pliki śledzone):** `.github/AGENT-PIPELINE.md`, `.github/agents/{coordinator,planner,implementer,verifier,stage-validator,code-reviewer,assumption-check,what-if,research-planner,information-gatherer-lite}.md`, `.github/skills/research-gatherer/SKILL.md`, `.github/instructions/{agent-workflow,session-continuity,mcp-routing,docs-index}.instructions.md`, `.github/context/{repo-index,agent-decisions,anti-patterns-critical}.context.md`, `tools/hooks/README.md`, `.claude/`.
- **Nie sprawdzone / nie zweryfikowane:** opis wymiany plikami między „Luną" a asystentem (relacja użytkownika, nie ma go w śledzonych plikach, które przejrzałem); treść agentów `bc-switch`, `adr-generator`, `spec-writer`, `copilot-setup-maintainer`.

---

## 1. Zasada przewodnia (z researchu)
- Wasz setup to **workflow z człowiekiem w pętli**, nie autonomiczni agenci (AGENT-PIPELINE: dwa punkty HITL, GO gate, limity iteracji). Anthropic: workflow dla zadań dobrze zdefiniowanych, agenci tam, gdzie liczba kroków jest nieznana; „adding complexity only when it demonstrably improves outcomes".
- Każdy z wzorców poniżej **dokłada koszt** (tokeny, opóźnienie, ryzyko pętli lub zepsucia wyniku). Dlatego każdy ma niżej swój **kompromis i z góry ustalone guardrails**.
- Wzorce się uzupełniają (uwaga użytkownika), ale źródła podkreślają też: gdy kopiujesz ten sam stan pięć razy, może to być „a supervisor in disguise" (Brad, swarm). Najtańsza wystarczająca kombinacja wygrywa.

## 2. Guardrails wspólne (ustalane PRZED uruchomieniem każdego kroku wieloagentowego)

Kolumna „Źródło": **S** = wynika ze źródeł (Brad/Anthropic), **P** = propozycja asystenta (liczby i progi do uzgodnienia, nie ze źródeł).

| ID | Guardrail | Przed czym chroni | Źródło |
|---|---|---|---|
| G1 | **Kto uruchamia:** tylko użytkownik, albo jawnie zdefiniowany wyzwalacz. Nic nie startuje „samo" | niespodziewany koszt | P (zgodne z GO gate i HITL) |
| G2 | **Limit rund/iteracji** zapisany w poleceniu (np. debata: ≤ 2 rundy) | pętle bez końca | S: Brad (`maxIterations`, enum werdyktu), Anthropic („maximum iteration count") |
| G3 | **Budżet tokenów** na krok i łączny; po przekroczeniu stop i raport. Punkt odniesienia z tej sesji: debata ≈ 0,52 mln tokenów subagentów | zjadanie tokenów | S: Anthropic (autonomia = koszt); liczby: P |
| G4 | **Stany wyjścia zdefiniowane z góry:** `consensus` / `continue z nazwanymi lukami` / `escalate do człowieka` / `stuck` | niekończące się „jeszcze jedna runda" | S: Brad (enum + stan `stuck`) |
| G5 | **Reguła braku postępu między turami:** stop, jeśli runda nie dała nowego zweryfikowanego dowodu ani żadnej korekty. Autor sam przyznaje, że jego no-progress guard „does not span turns", więc trzeba ją zdefiniować osobno | zapętlenie bez efektu | S (ostrzeżenie), reguła: P |
| G6 | **Zużyj wyzwalacz, zachowaj dowody:** zamknięte kwestie są oznaczane, żeby nie wyzwalały kolejnej rundy; krytyki są czyszczone, stanowiska zostają. Inaczej „loop that runs and doesn't debate" | pusta runda | S: Brad (accumulate vs consume) |
| G7 | **Dodatkowe pobranie z sieci/kodu tylko na nazwane luki** (≤ 3 na rundę), z listą dozwolonych źródeł; niepowodzenie = stop i pytanie do człowieka, nie ponawianie w pętli | rozjeżdżanie się zakresu, koszt sieci | P |
| G8 | **Najpierw deterministyczny check, potem sędzia LLM** (build/testy, skrypt weryfikacji cytatów, zgodność z `anti-patterns-critical`) | sędzia z biasami zamiast faktów | S: Zheng (biasy), Huang (samokorekta bez zewnętrznego sygnału) |
| G9 | **Niezależność:** krytycy dostają ten sam materiał, ale nie widzą swoich ocen; różne perspektywy; różne rodziny modeli, gdy są dostępne. Zgoda na wspólnej bazie ≠ potwierdzenie | wspólny bias i pozorny konsensus | S: Brad (correlated errors), nasz sędzia debaty |
| G10 | **Bramka człowieka** przed: startem, pobraniem spoza repo, zapisem poza dozwolonymi ścieżkami (GO gate) | niechciane skutki uboczne | P (zgodne z `tools/hooks/README.md`) |
| G11 | **Raport kosztu po kroku:** tokeny, liczba rund, co skorygowano | brak widoczności opłacalności | P |

## 3. Wzorzec po wzorcu

Skróty: **Mamy** = widoczne w śledzonych plikach; **Mamy częściowo**; **Nie mamy**. „Rodzaj" = jak używać: **flow** (stały element), **na prośbę** (tylko gdy użytkownik zażąda), **doradczo** (agent proponuje, człowiek zatwierdza).

### 3.1 Supervisor / Router (Good for: tool-rich assistants, specialist agent teams, customer support, coding agents, centralized control)
- **Stan:** *Mamy częściowo.* Routing jest **dokumentowy, nie agentowy**: front door `agent-workflow.instructions.md` → tabela `docs-index.instructions.md` → reguły `mcp-routing.instructions.md` (RAG / KG / narzędzia bezpośrednie) → `repo-index.md` jako mapa kodu. Aktywny supervisor w sensie Brada (agent wybierający następnego specjalistę typowanym enumem) jest tylko w `coordinator` (z jawną mapą delegacji, dla frameworku Architect Consultant).
- **Gdzie warto:** tam, gdzie wybór specjalisty jest powtarzalny i ma skończoną listę (np. „jakim agentem/skillem to obsłużyć"). Brad: wybór jako wartość z enumu, nie wolny tekst; polityka routingu widoczna przed uruchomieniem.
- **Gdzie nie:** tam, gdzie tablica dokumentów już rozstrzyga; agent-router dodaje wywołanie modelu za każdym razem.
- **Ryzyko / guardrails:** bad routing zatruwa zadanie (enum zawęża); supervisor jako wąskie gardło; G2, G4 (wyjście `escalate`). Ostrzeżenie Brada: ponowne trasowanie do tego samego specjalisty bywa „no-op", jeśli stary stan nie został zużyty (G6).
- **Rodzaj:** flow (dokumentowy, już jest) + doradczo (agent proponuje trasę w planie).

### 3.2 Fan-Out / Fan-In (Good for: research z wielu źródeł, niezależna analiza, generowanie alternatyw, szerokie porównania, duże zadania podzielne)
- **Stan:** *Mamy.* `research-gatherer` rozdziela zbieranie na kategorie źródeł (w tej sesji: 6 gathererów równolegle); `coordinator` dopuszcza delegację równoległą „only where steps have no dependency".
- **Uwaga użytkownika:** użyć go **jako sprawdzenie podziału zadania**: czy zadania zadeklarowane dla agenta nie są zbyt obszerne albo zbyt poszatkowane. Wynik jako **sugestia do użytkownika z uzasadnieniem**, nie decyzja.
- **Ocena:** to pokrywa się z pierwszym punktem awarii z samego źródła: „Bad decomposition creates overlapping or missing work", a Brad zaleca, by dekompozycja była **staged, inspectable field**, który można zrecenzować przed rozgałęzieniem. Czyli pomysł użytkownika = rekomendowana mitygacja, nie wymysł.
- **Możliwe kryteria „za obszerne / za drobne" (P, do uzgodnienia):** za obszerne: jedno zadanie dotyka wielu BC albo wielu niezależnych decyzji; za drobne: dwa zadania zmieniają te same pliki lub wymagają tego samego kontekstu (koszt powielony, ryzyko konfliktu); rozmiar kontekstu na robotnika.
- **Gdzie nie:** edycje kodu na wspólnych plikach (zależności, konflikty), małe zadania (narzut > zysk).
- **Ryzyko / guardrails:** koszt rośnie liniowo z liczbą robotników; „one slow worker holds the join" (Brad: join jako predykat, nie czekanie na wszystkich); G3 (budżet), limit liczby robotników (research-gather ma limit 8 instancji), każdy robotnik musi zapisać plik, nawet pusty.
- **Rodzaj:** flow w researchu; doradczo przy deklarowaniu zadań.

### 3.3 Sequential Pipeline (Good for: naturalna kolejność, research → design → implementation → review, ETL, generowanie dokumentów)
- **Stan:** *Mamy* (to rdzeń: planner → implementer → pr-commit; fazy plan/walidacja; fazy research-gather).
- **Uwaga użytkownika:** dobrze działa razem z fan-out/fan-in (etap sekwencyjny z równoległym podetapem).
- **Ryzyko:** „Early mistakes compound downstream" (źródło). Guardrails: bramki między etapami (HITL 1, HITL 2), `stage-validator` binarny, G8.
- **Rodzaj:** flow. Brad dodaje: pętla powrotna z notatkami zamiast poprawiania „na ślepo".

### 3.4 Hierarchical Manager / Worker (Good for: duże zadania, wiele domen, wielu wyspecjalizowanych agentów)
- **Stan:** *Mamy częściowo.* `coordinator` → 5 agentów governance + agenci wykonawczy; w tej sesji menedżer + gatherzy (osobne konteksty). Podagenci w Claude Code to „własny store na poziom" z opisu Brada: dyrektor widzi brief w dół i wynik w górę, nie transkrypt.
- **Gdzie warto:** zadania wielodomenowe, gdzie jeden kontekst by się zapchał (np. przełączenie BC, duża zmiana przekrojowa).
- **Gdzie nie:** małe zmiany; trzeci poziom. Brad: głębokość to decyzja projektowa, „adding a layer is a code review".
- **Ryzyko / guardrails:** koszt i rozmywanie odpowiedzialności; zasada: ≤ 2 poziomy (P), delegacja tylko do zarejestrowanych agentów (zgodne z `coordinator`: „do not invent a new agent"), G3.
- **Rodzaj:** doradczo (plan wskazuje, czy zadanie wymaga hierarchii).

### 3.5 Peer Handoff / Swarm (Good for: dynamiczne workflow, systemy konwersacyjne, luźno powiązani specjaliści, ścieżka nieznana z góry)
- **Stan:** *Nie mamy.* Wasz styl to jawne bramki, a nie przekazywanie sterowania między równorzędnymi.
- **Gdzie mógłby pasować:** eksploracyjna diagnoza (np. utknięta saga: specjalista od logów, bazy, kodu przekazują dalej), bez zapisu; później ewentualnie konwersacyjny czatbot sklepu.
- **Ryzyko:** pętle przekazań, niejasny właściciel, trudny do przewidzenia przebieg. Brad: stan `stuck` + `maxIterations`; no-progress guard **nie** łapie pętli między turami. Źródło sam ocenia, że mesh to „price of making the topology explicit".
- **Guardrails:** G2, G4 (`stuck`, `escalate`), G5, zapis każdego przekazania wraz z powodem (dziennik trasy), tylko odczyt.
- **Wniosek:** pilot ewentualny, **nie** do zadań z edycją. Wartość niepotwierdzona danymi.
- **Rodzaj:** na prośbę.

### 3.6 Debate / Consensus (Good for: niejednoznaczne rozumowanie, decyzje architektoniczne, ocena hipotez, przegląd adwersaryjny, redukcja ślepych punktów)
- **Stan:** *Nie mamy* jako stałego kroku. Jednorazowo użyliśmy go w tej sesji (3 perspektywy + krytyki + sędzia).
- **Uwaga użytkownika (nowy pomysł):** debata **w researcherze, na prośbę użytkownika**, żeby przegadać wyniki końcowe i dać lepszy rezultat. Rozszerzenie: ta sama debata może **zainicjować ponowne pobranie** czegoś z sieci, z kodu lub z innego miejsca.
- **Ocena:** to ma sens, jeśli sędzia zwraca nie tylko „kontynuuj", ale **nazwane luki** (u Brada: `judge_note` = „what is still contested, specifically"). Wtedy dodatkowe pobranie jest wąskie i ograniczone, a nie „poszukaj więcej". W tej sesji właśnie takie luki wyszły z debaty (ReAct jako źródło limitów, Zheng przypisany do reducera, itd.).
- **Dowód z tej sesji:** z 7 opisanych przez sędziego twierdzeń 5 było błędnie przypisanych lub przesadzonych, w tym jedno z mojej własnej syntezy (poprawione: erratum E1). Koszt ≈ 0,52 mln tokenów. **Ograniczenia:** debaterzy i sędzia to modele jednej rodziny, jedna baza dowodów; brak próby kontrolnej.
- **Czego pilnować (problemy tego rozwiązania):**
  - **Pętla** „jeszcze runda" → G2, G4, G5.
  - **Pusta runda:** krytyki z poprzedniej rundy nie wyczyszczone → G6.
  - **Koszt:** Brad podaje wzór (debaterzy × rundy × (pozycje+krytyki)) + sędzia → G3.
  - **Pozorny konsensus** na wspólnym bias → G9; debata nie dowodzi poprawności.
  - **Wynik gorszy zamiast lepszego:** sędzia LLM z biasami (Zheng) → G8: najpierw deterministyczne checki (np. weryfikacja cytatów skryptem).
  - **Zakres pobierania:** G7.
- **Rodzaj:** **na prośbę** użytkownika (G1), nigdy automatycznie.

### 3.7 Blackboard / Shared-State (Good for: zadania długie, agenci asynchroniczni, wspólna pamięć, agenci mogą dołączać i odchodzić)
- **Uwaga użytkownika (z praktyki):** wymiana między „Luną" a asystentem przez pliki: Luna rozwiązywała i wpisywała, co się nie powiodło, asystent edytował **drugi plik** z prawdopodobnym rozwiązaniem, Luna z tego korzystała i szła dalej. Słabszy model mógł poczekać, a silniejszy reagował na zmiany. Według użytkownika działało „naprawdę świetnie". *Nie zweryfikowałem tego w śledzonych plikach.*
- **Mapowanie na wzorzec:** to klasyczny blackboard: pliki jako tablica, **jeden plik na piszącego** (odpowiednik klucza autora u Brada; unika konfliktów edycji), fakty dopisywane, hipotezy zmienne. Rolę kontrolera pełnił człowiek (przekazywanie między sesjami).
- **Gdzie warto:** praca asynchroniczna między różnymi modelami/narzędziami (Copilot + Claude Code), gdzie wspólny stan musi przeżyć granicę sesji (`session-continuity` robi to dla planów).
- **Ryzyko (z opisu wzorca):** zły wpis zatruwa wszystkich; nieaktualne odczyty; brak gwarancji kolejności bez kontrolera; zapętlenie przy wzajemnych reakcjach.
- **Guardrails (P):** zapis tylko dopisywany (append-only), jeden plik na piszącego, wpis z identyfikatorem/datą/statusem („otwarte/rozwiązane" = zużycie wyzwalacza, G6), limit rozmiaru, człowiek jako kontroler lub jawny limit reakcji (G2).
- **Rodzaj:** na prośbę / doraźnie (już działało).

### 3.8 Planner / Executor (Good for: otwarte cele, kodowanie, workflow badawcze, ścieżka nie do przewidzenia, długa praca z ponownym planowaniem)
- **Stan:** *Mamy.* `planner` (read-only, STOP na HITL 1) → `implementer` wykonuje zatwierdzony plan. Krawędź ponownego planowania istnieje: implementer „STOP, return to `@planner` with a revision request", a przy wyczerpanych próbach: `REVISE PLAN` do plannera. Limity: planner 3 rewizje, implementer 5 przejść, 3 próby weryfikacji.
- **Różnica względem Brada:** u was ponowne planowanie rozstrzyga człowiek (nie enum maszyny), a historia cykli nie jest trwale zapisana (plany w `.github/plans/` są domyślnie ignorowane przez git i kasowane po walidacji).
- **Ryzyko:** zapętlone re-planowanie → guardrails już są (limity); dla pełni G5.
- **Rodzaj:** flow (jest).

### 3.9 Generator / Critic / Refiner (Good for: generowanie kodu, pisanie, przeglądy architektury, compliance, jakość strukturalnych wyjść, jasne kryteria akceptacji)
- **Stan:** *Mamy częściowo*, z istotnym niuansem: krytyk **wbudowany w implementera** (inline anti-pattern scan, checki 1–8) to ten sam agent, który napisał kod. Źródło (Brad) podkreśla asymetrię ról; literatura o biasach sędziów wskazuje na self-enhancement. Niezależność dają: **deterministyczne próby** (build + testy), osobna sesja walidacji (`session-continuity`: „ideally a different model") oraz standalone `code-reviewer`.
- **Ryzyko:** krytyk wymyśla nieistotne problemy; plateau jakości; generator optymalizuje pod krytyka. Brad: budżet iteracji z danych i stan `stalled`. U was: limit 3 prób, potem human.
- **Rodzaj:** flow (jest), z asymetrią słabszą w części „review inline".

### 3.10 Auction / Contract-Net (Good for: duże heterogeniczne pule agentów, optymalizacja kosztu, routing modeli)
- **Pytanie użytkownika:** gdzie to zastosować; może przy debacie, gdzie agenci „licytują", kto da więcej?
- **Ocena (uczciwa):** **nisko opłacalne dziś.** Powody: (1) pula agentów/modeli jest mała i znana, więc routing statyczny wystarcza (`context-cost-analysis`: tańsze modele do eksploracji, droższy do implementacji); Brad: „if the overhead isn't worth it… you route statically". (2) Oferty to **samoopis pewności**, która bywa źle skalibrowana (Brad: „calibration problem"); w debacie licytacja pewnością mogłaby wzmacniać przeświadczonego, ale błędnego agenta.
- **Wariant potencjalnie użyteczny:** lekki „bid" jako **estymata przed zadaniem** (co agent potrzebuje, ryzyka, ocena pewności), służąca człowiekowi do wyboru. Bez automatycznego rozstrzygania.
- **Rodzaj:** nie teraz; ewentualnie doradczo.

### 3.11 Event-Driven / Publish-Subscribe (Good for: długie systemy, asynchroniczne workflow, monitoring i reaktywna automatyzacja, agenci działają tylko przy istotnej zmianie)
- **Uwaga użytkownika:** już mieliśmy (plik + reagowanie na zmiany); pewnie są lepsze sposoby, czeka na propozycje.
- **Propozycje (konceptualne, bez implementacji):**
  1. **To, co już robiliście:** plik jako zdarzenie, człowiek jako wyzwalacz. Działa, bo kontrolę ma człowiek.
  2. **Zdarzenia z narzędzia:** Claude Code ma hooki zdarzeń (u was: `UserPromptSubmit`, `PreToolUse`); zdarzenie „po wykonaniu narzędzia" mogłoby zapisywać ślad, na który reaguje inny agent. Zysk: reaktywność bez ręcznego przekazywania. Koszt: złożoność i ryzyko pętli.
  3. **Kolejka/szyna:** przesada dla workflow deweloperskiego.
- **Ostrzeżenie ze źródła:** sam autor pisze, że w jego implementacji „emit" tylko loguje, a wejście zewnętrzne nie istnieje, więc „internal event system" jest prawdziwy, a zewnętrzny to zamiar. Nie traktować tego wzorca jako gotowej, sprawdzonej technologii.
- **Guardrails:** „consume the event" (G6), jawnie zadeklarowane krawędzie, G2, brak reakcji reakcji (brak cykli A→B→A).
- **Rodzaj:** doraźnie (plik + człowiek); automatyka dopiero po wykazaniu wartości.

### 3.12 Quorum / Voting (Good for: decyzje wymagające wysokiej pewności, klasyfikacja, walidacja, redukcja wariancji pojedynczego modelu, bramki bezpieczeństwa/jakości)
- **Uwaga użytkownika:** dobre przy przedstawianiu rozwiązania, gdy agenci decydują, co jest dobre.
- **Ocena:** dobre dla **wyników binarnych lub enumowych** (np. „czy to BLOCKS MERGE?", „jaki typ zadania?"), gdzie niezależne głosy mają sens; słabe dla otwartych ocen jakości rozwiązania (to rola debaty, nie głosu).
- **Ryzyko:** skorelowane błędy (jedna rodzina modeli = ta sama ślepa plamka; Brad: „confidently wrong"), koszt ×N, większość ≠ poprawność.
- **Guardrails:** 3 głosujących, każdy w osobnym kontekście bez wglądu w cudze głosy, różne rodziny modeli gdy dostępne (u was: modele z różnych dostawców, według relacji użytkownika), tylko enum, 1 runda, remis = człowiek (G9, G2). Konieczny **deterministyczny check przed głosowaniem** (G8).
- **Rodzaj:** na prośbę, dla konkretnych bramek.

### 3.13 Dynamic Team Formation (Good for: duże rejestry agentów, heterogeniczne umiejętności, wymagania wyłaniają się w trakcie, różne umiejętności na różnych etapach)
- **Uwaga użytkownika:** nie stosowaliście, ale wydaje się, że gdzieś można.
- **Stan:** *Nie mamy.* `coordinator` ma **statyczną** mapę delegacji i zakaz wymyślania nowych agentów. Rejestr istnieje (20 agentów w `.github/agents/`, 2 w `.claude/agents/`, kilkadziesiąt skilli w `.github/skills/`).
- **Gdzie warto:** gdy zadanie wymaga innego składu za każdym razem (np. zmiana przekrojowa: ADR + test + migracja + dokumentacja). Wzorzec Brada: „rejestr jako typ": skład to lista wartości z **zamkniętego zbioru istniejących agentów**; zbyt duża lista jest odrzucana (u Brada: > 4 → ponów dobór).
- **Ryzyko:** zły dobór składu, zbędni specjaliści. Guardrails: skład tylko z istniejącego rejestru, limit rozmiaru (P: ≤ 4), **skład zatwierdza człowiek w HITL 1** (to w praktyce element planu), G3.
- **Rodzaj:** doradczo.
- **Związek z aukcją:** w źródle autor sam zauważa, że aukcja i formowanie zespołu to ta sama mechanika w różnej arności (wybór jednego vs. skład). Skoro aukcja jest niskiej wartości, formowanie składu jako **doradcza propozycja** jest tańsze od licytacji.

## 4. Jak wzorce się uzupełniają (kombinacje, które mają sens)

| Kombinacja | Po co | Uwaga o koszcie/ryzyku |
|---|---|---|
| Sequential + Fan-Out/In | etapy z równoległymi podetapami (research: plan → gatherzy równolegle → merge) | N× koszt w etapie równoległym; G3 |
| Planner/Executor + Generator/Critic | plan zatwierdzony, wykonanie z krytykiem i bramką deterministyczną | to właśnie macie; słabość: krytyk inline |
| Supervisor + Hierarchical | wybór specjalisty + osobne konteksty | ≤ 2 poziomy (P) |
| Debate + Quorum | debata ujawnia argumenty, głosowanie rozstrzyga binarnie | koszt sumuje się; używać tylko dla ważnych decyzji |
| Blackboard + Event-driven | wspólny stan + reakcja na zmiany | ryzyko pętli reakcji; wymaga „consume" i limitu |
| Dynamic Team + Hierarchical | skład dobierany, praca delegowana do własnych kontekstów | wyższa złożoność; tylko duże zadania |

## 5. Pytanie otwarte użytkownika: wzorce „per zadanie" i decyzja agenta

**Pomysł użytkownika:** część wzorców wbudować w flow na stałe; część zostawić agentowi, który podejmie decyzję, gdzie czego użyć przy konkretnym problemie.

**Moja ocena (hipoteza, nie wynik pomiaru):**
- **Wartościowe jako krok doradczy.** Agent, mając zadanie, **proponuje kształt** (np. „jedno zadanie sekwencyjne", „fan-out z 3 robotnikami", „debata dla sporu X") wraz z **oszacowaniem kosztu i guardrails**, a człowiek zatwierdza w tym samym punkcie, w którym zatwierdza plan (HITL 1). To spójne z waszym stylem pracy i z tym, że „plan" już dziś zawiera ryzyko i kroki.
- **Ryzyko do zadeklarowania z góry:** katalog wzorców **sam podpowiada użycie złożonych wzorców** (efekt słownictwa), więc agent może zawyżać skomplikowanie. Przeciwwaga: **domyślnie najprostszy kształt**, a każde „podniesienie" wymaga nazwanego powodu i szacunku kosztu (zasada Anthropic: złożoność tylko gdy poprawia wynik).
- **Nie zostawiać agentowi:** autonomicznego uruchamiania kosztownych wzorców (debata, quorum, swarm) bez zgody (G1, G10).
- **Co byłoby dowodem, że to się opłaca (bez implementacji):** retrospektywne porównanie: dla kilku znanych zadań, czy proponowany kształt różnił się od faktycznie użytego i czy różnica miałaby uzasadnienie. Dane o skuteczności takiego doradcy w źródłach **nie istnieją**.

## 6. Rodzaje użycia: zestawienie (stan na dziś, wszystko hipotezy)

| Wzorzec | Mamy? | Rodzaj | Priorytet rozważenia (ocena asystenta) |
|---|---|---|---|
| Sequential | tak | flow | trzymać |
| Planner/Executor | tak | flow | trzymać |
| Generator/Critic | częściowo (krytyk inline) | flow | doprecyzować niezależność tam, gdzie ryzyko wysokie |
| Supervisor/Router | częściowo (routing dokumentowy) | flow + doradczo | rozważyć jawny wybór trasy w planie |
| Fan-Out/Fan-In | tak (research) | flow + doradczo | **rozważyć jako sprawdzenie podziału zadań** (pomysł użytkownika) |
| Hierarchical | częściowo | doradczo | tylko duże, wielodomenowe zadania |
| Debate | nie (użyte raz) | na prośbę | **rozważyć dla researchera** (pomysł użytkownika) z guardrails G1–G11 |
| Blackboard | doraźnie (relacja użytkownika) | na prośbę | rozważyć sformalizowanie zasad pliku wspólnego |
| Event-driven | doraźnie | doraźnie | nie automatyzować przed dowodem wartości |
| Quorum | nie | na prośbę | wąsko: bramki binarne |
| Dynamic Team | nie | doradczo | rozważyć jako część planu |
| Swarm | nie | na prośbę | tylko diagnostyka tylko-do-odczytu, niska pewność |
| Auction | nie | — | nisko opłacalne dziś |

## 7. Ograniczenia
- Materiał o wzorcach to w ~90% jeden autor (Brad), bez niezależnej weryfikacji jego runtime'u; Anthropic i literatura (Zheng, Huang) potwierdzają tylko wybrane tezy kierunkowo.
- Liczby (limity rund, rozmiar składu, budżety) są **propozycjami do uzgodnienia**, nie ustaleniami ze źródeł.
- Tabela „Mamy / nie mamy" opiera się na plikach, które przejrzałem; nie czytałem wszystkich agentów i skilli w całości.
- Relacja o wymianie przez pliki między „Luną" a asystentem pochodzi od użytkownika i nie była weryfikowana w repo.

---

## 8. Doprecyzowanie użytkownika: autonomia dobierana z kontekstu, nie bramki na wszystko

**Uwaga użytkownika (2026-10-14, po §5–§7):** nie blokować wszystkiego. Agent ma **znać wzorce, ich możliwości i znane problemy** i **dopasowywać je do problemu z kontekstu, bez zbędnego czekania**, także do **części zadania**, ale nie w stylu „100% yolo". Ma przy tym patrzeć na **bieżące zużycie tokenów** (token efficiency). Wzorców nie używa się zawsze i na pewno nie do poprawki formularza.

**Ocena asystenta:** zgadzam się z kierunkiem. Doprecyzowuje to i częściowo koryguje §5 (tam „agent proponuje, człowiek zatwierdza" było zbyt bramkujące dla tanich, odwracalnych kroków) oraz kolumnę „Rodzaj" w §3/§6.

### 8.1 Zasada: ograniczać (caps), nie pytać (gates), gdy krok jest tani i odwracalny
Człowiek jest wąskim gardłem; pytanie o każdy krok łamie sens token efficiency i płynności pracy. Zamiast tego:
- **Limity domyślne zawsze obowiązują** (G2 limit rund, G3 budżet, G5 brak postępu, G4 wyjścia). Kosztują zero i niczego nie blokują.
- **Pytanie człowieka zostaje tylko tam, gdzie koszt jest wysoki albo skutki są nieodwracalne** (poziom C poniżej).

### 8.2 Trzy poziomy autonomii (progi: **propozycja**, do uzgodnienia)

| Poziom | Kiedy | Przykłady | Co robi agent |
|---|---|---|---|
| **A: od ręki** | koszt mały, tylko odczyt, w granicach repo, w pełni odwracalne | check deterministyczny przed oceną LLM (build, testy, weryfikacja cytatów); jeden krytyk na ryzykowny fragment wniosku lub diffu ze sprawdzaniem źródeł; krótkie sprawdzenie podziału zadania (fan-out sanity) w jednym zdaniu; 2–3 równoległe sondy odczytowe dla niezależnych pytań | robi i **w jednej linii** podaje: jaki wzorzec, dlaczego, przybliżony koszt |
| **B: powiedz i działaj** | koszt umiarkowany, tylko odczyt; użytkownik może przerwać | fan-out > 3 robotników; pobranie z sieci w ramach już zatwierdzonego planu/źródeł; walidacja drugą rodziną modeli dla ryzykownej zmiany; podział większego zadania na fazy | ogłasza jednym zdaniem z szacunkiem i **nie czeka** na odpowiedź; stosuje G2–G5 |
| **C: zapytaj najpierw** | koszt wysoki lub skutki uboczne | pełna debata (w tej sesji ≈ 0,5 mln tokenów), quorum wielomodelowe, swarm, cokolwiek co zapisuje poza dozwolonymi ścieżkami (GO gate), nieodwracalne działania, pobranie spoza zatwierdzonej listy, kroki przekraczające budżet | prosi o zgodę przed startem (to jest pomysł użytkownika „debata na prośbę") |

Poziom wynika z trzech osi: **koszt** (tokeny), **odwracalność/skutki**, **niepewność** (jak sporny jest wniosek i jak kosztowny błąd).

### 8.3 Kiedy w ogóle NIE rozważać wzorców (domyślnie, zero narzutu)
Mały zakres (1–2 pliki), jasna specyfikacja, znany sposób naprawy, odwracalne (np. poprawka formularza). **Nie ma wtedy „kroku wyboru wzorca"**: sam wybór kosztuje tokeny i musi być pomijalny dla małych zadań.

### 8.4 Sygnały, że wzorzec może się opłacić (heurystyki, P)
| Sygnał w zadaniu | Kandydat |
|---|---|
| kilka niezależnych pytań/obszarów do zbadania | fan-out/fan-in (z kontrolą podziału) |
| wniosek ma wpływ na kosztowną decyzję albo jest sporny | pojedynczy krytyk → dopiero potem debata (poziom C) |
| duże ryzyko / duży promień rażenia zmiany | niezależna walidacja (inny model/sesja) |
| zadanie przekracza jedną sesję | fazy plan/walidacja (już macie) |
| wspólny stan między modelami/narzędziami | blackboard przez pliki |
| zadanie wymaga innego składu specjalistów | dobór składu z istniejącego rejestru |
| binarna decyzja o wysokiej stawce | quorum (3 niezależne głosy), poziom C |

### 8.5 Świadomość tokenów
- Agent powinien znać **bieżące zużycie i pozostały budżet**, jeśli narzędzie to udostępnia. Asystent w Claude Code widzi liczbę pozostałych tokenów sesji; **nie wiem, czy Copilot to udostępnia**, więc reguła musi działać też bez tej wiedzy (oszacowanie klasy kosztu S/M/L).
- Reguła (P): jeśli szacowany koszt wzorca jest duży względem pozostałego budżetu, **podnieś poziom o jeden** (A→B, B→C) albo wybierz tańszy wariant (pojedynczy krytyk zamiast debaty).
- **Wiedza o wzorcach też kosztuje tokeny.** `context-cost-analysis.md` pokazuje stały blok ≈ 7 110 tokenów na każdy request. Pełny opis 13 wzorców nie może wchodzić do stałego kontekstu; potrzebna jest **zwięzła karta decyzyjna** (kiedy / kiedy nie / koszt / stop) ładowana tylko gdy zadanie przekracza próg „mały zakres". Rząd wielkości (P, szacunek): ok. 40 tokenów na wzorzec, czyli ok. 500 tokenów na całość.

### 8.6 Ryzyka tego podejścia (do ujawnienia z góry)
1. **Zła kalibracja.** Agent może zawyżać (efekt młotka: „znam wzorzec, więc go użyję") albo zaniżać. Nie ma danych o skuteczności takiego doboru. Przeciwwaga: **domyślnie brak wzorca**, wymagany nazwany powód i szacunek kosztu w jednej linii.
2. **Koszt samej decyzji.** Rozważanie wzorców przy każdym zadaniu zjada tokeny. Dlatego §8.3 jest ważniejsze niż cała tabela sygnałów.
3. **Słabsze modele** (np. tańsze „included" w Copilot) gorzej oceniają takie kompromisy. Reguła musi być prosta: kilka sygnałów, jasne domyślne wartości, wyższe poziomy zarezerwowane dla człowieka.
4. **Cichy dryf.** Jeśli agent nie zostawia śladu decyzji, nie da się nauczyć kalibracji. Jedna linia „Wzorzec: … (powód, koszt)" wystarcza, by użytkownik mógł skorygować i by po czasie dało się ocenić trafność.
5. **Pozorna niezależność** (wspólna rodzina modeli, wspólna baza dowodów) nie znika dlatego, że decyzję podjął agent, a nie człowiek (G9).

### 8.7 Aktualizacja oceny asystenta (zastępuje wcześniejszą wersję w rozmowie)
- **Największa dźwignia:** twierdzenia dające się zweryfikować, czyli deterministyczny check przed oceną LLM. W tej sesji najtańszy element (skrypt porównujący cytaty) dał najwięcej.
- **Setup jest dobry** (workflow z człowiekiem w pętli). Najsłabsze miejsce: krytyk wbudowany w implementera to ten sam agent; niezależność dają testy i osobna sesja. Nie mierzyłem trafności BLOCKS MERGE.
- **Najlepsze pomysły użytkownika:** kontrola podziału zadania (fan-out sanity), debata w researcherze **dla spornych, kosztownych wniosków** (poziom C) z nazwanymi lukami i ograniczonym pobraniem, blackboard przez pliki między dwiema rodzinami modeli (rzadka realna niezależność; w naszej debacie wszyscy byli z jednej rodziny).
- **Do odpuszczenia:** aukcja, swarm przy edycjach, runtime Brada.
- **Wybór wzorca z kontekstu:** tak, ale jako **ograniczona autonomia** (§8.2), nie jako bramka na każdy krok i nie jako 100% swoboda. Debata pełna zostaje „na prośbę"; jej lekka wersja (jeden krytyk + deterministyczny check) może być na poziomie A.
- **Niepewność:** n = 1, bez grupy kontrolnej; mogę przeceniać weryfikację przez efekt świeżości; liczby to propozycje; nie przeczytałem wszystkich agentów.

---

## 9. Dynamiczny workflow na poziomie setupu: czy da się to wpasować

**Uwaga użytkownika (2026-10-14):** na tej podstawie da się zrobić naprawdę dynamiczny workflow **bez zabawy w ściąganie cudzych rozwiązań** i bez twierdzeń, że coś jest „najlepsze". Mamy **decyzyjność na poziomie setupu**; taki setup jest według użytkownika dojrzalszy i daje **samoświadomość**, co będzie jeszcze lepsze. Prośba: przeanalizować, czy jesteśmy w stanie to wpasować, i dopisać do raportu.

**Czego nie potwierdzam:** że nasz setup jest „dojrzalszy" lub „lepszy" od cudzych rozwiązań. Żadne ze zebranych źródeł nie porównuje setupów, a my nie mierzyliśmy wyników. Dojrzałość **w zarządzaniu** (bramki, ADR, pamięć, limity, model kosztów) widać w plikach. Dojrzałość **w wynikach** jest niezmierzona. Poniżej: co jest wsparte dowodem, a co jest hipotezą.

### 9.1 Co oznacza „dynamiczny workflow na poziomie setupu"
Decyzje o **kształcie pracy** (jaki agent, jaki wzorzec, jak głęboko, jaki budżet) podejmuje **własna warstwa decyzyjna setupu**, korzystając z jego rejestru, polityk, modelu kosztów i pamięci, zamiast stałego pipeline'u albo grafu narzuconego przez zewnętrzny framework.

### 9.2 Co wspiera ten kierunek (źródła)
- **Anthropic** (pobrane 2026-10-14): frameworki pomagają zacząć, ale mogą ukrywać prompty i odpowiedzi; zaleca się najpierw używać API bezpośrednio i rozumieć kod frameworka. *To parafraza z podsumowania narzędzia pobierającego, nie dosłowny cytat.* Dodatkowo: „adding complexity only when it demonstrably improves outcomes".
- **Nasze strony AutoGen** ([strony AutoGen, lista w SOURCES.md](../research/2026-10-14-agent-patterns-bradmurry/SOURCES.md)) to **przykłady referencyjne**: znaleziono w nich rozbieżności między prozą a kodem (m.in. refleksja bez limitu iteracji w kodzie; group chat „not meant to be used in real applications"). Kopiowanie takich wzorców „jak leci" nie jest bezpieczniejsze niż własna warstwa decyzyjna. Zobacz `analysis/01-cross-source-verification.md` §1 (C7–C12).
- **Brad:** jego własny runtime to rozwiązanie jednego autora bez publicznej weryfikacji, więc „ściągnięcie" go nie byłoby bezpieczniejsze.

**Wniosek:** kierunek „własna warstwa decyzyjna zamiast importu frameworka" jest **rozsądną decyzją projektową, niesprzeczną ze źródłami**. To nie dowód, że będzie lepszy: oznacza tylko, że import nie ma przewagi dowodowej.

### 9.3 Z czego już składa się taki setup (stan na 2026-10-14, pliki śledzone)

| Potrzeba warstwy decyzyjnej | Co jest | Gdzie | Ocena pod „dynamiczny" |
|---|---|---|---|
| Rejestr zdolności | 20 agentów (opisy i frazy wyzwalające), 44 skille, 17 promptów | `.github/agents/`, `.github/skills/`, `.github/prompts/`; spis liczbowy w `.github/setup-state.md` (21 instrukcji, 17 promptów, 20 agentów, 44 skille, 33 ADR, 8 plików kontekstu) | jest, opisy zawierają wskazówki trasowania |
| Routing | front door, tabela dokumentów, reguły MCP | `agent-workflow.instructions.md` („Infer the path from task shape and target files"), `docs-index.instructions.md`, `mcp-routing.instructions.md` | jest, **statyczny i dokumentowy** |
| Polityki / guardrails | GO gate, `safety`, `anti-patterns-critical`, limity iteracji per agent, HITL | `tools/hooks/`, `.claude/settings.json`, pola `max-iterations` agentów | jest |
| Pamięć | log korekt z regułą promocji, stan projektu, znane błędy | `agent-decisions.md` (362 linie), `project-state.md`, `known-issues.md` | jest, ale zapisuje **korekty**, nie wyniki doboru |
| Model kosztów | szacunek stałego bloku i kosztu tur | `context-cost-analysis.md` (z 2026-06-03, oparty na założeniach) | jest, **niezmierzony** |
| Samoopis setupu | liczby inwentarza; agenci audytu setupu (`copilot-setup-maintainer`, `setup-discovery`) | `setup-state.md` (7 linii), agenci | **częściowo**: opisuje inwentarz, nie możliwości, koszty i ograniczenia |
| Wiedza o wzorcach | brak jej w setupie; powstała dopiero w tym researchu | `docs/research/…`, `docs/reports/…` | **brak** (nie ma karty decyzyjnej) |
| Historia i pomiar doboru | brak zapisu „co wybrano i jak poszło" | — | **brak** |

**Co już dziś jest „dynamiczne":** wybór trasy i narzędzia z kształtu zadania (instrukcja każe wnioskować ścieżkę, nie czekać na nazwę narzędzia) oraz delegacja w `coordinator`. Nowy byłby **wybór kształtu workflow (wzorca)** i **świadomość kosztu**.

### 9.4 „Samoświadomość": co to znaczy i w jakim stopniu ją mamy
Używam tego jako metafory **modelu własnego setupu zapisanego w plikach** (model nie uczy się między sesjami; pamięcią są pliki). Trzy warstwy (hipoteza robocza asystenta):

| Warstwa | Pytanie, na które odpowiada | Dziś |
|---|---|---|
| **Wiedza** | co potrafię, ile kosztuje, jakie mam znane problemy (np. wzorzec debaty: koszt, ryzyko pozornego konsensusu) | częściowo (rejestr agentów); brak wiedzy o wzorcach i ich kosztach |
| **Stan** | ile budżetu zostało, jak ryzykowne jest to zadanie, co jest zablokowane | częściowo (`project-state.md`, bramki); brak jednolitego dostępu do zużycia tokenów w obu narzędziach |
| **Historia** | co wybrałem poprzednio i jak to wyszło | brak |

Jakość „samoświadomości" = jakość tych plików + to, czy agent faktycznie je czyta w odpowiednim momencie. To ma **cenę w tokenach** (stały blok ≈ 7 110 tokenów) i ryzyko **dryfu dokumentów** (istnienie `copilot-config-sync.instructions.md` świadczy, że synchronizacja jest już realnym kosztem).

### 9.5 Napięcie, które trzeba nazwać
Teza serii Brada i naszych źródeł brzmi: **problem powstaje, gdy sterowanie przepływem żyje w modelu**. Pomysł „agent dobiera wzorzec" przenosi **część** decyzji o przepływie do modelu. To nie wyklucza pomysłu, ale określa warunki:
- wybór ma być **typowany i zamknięty**: agent wybiera **spośród zadeklarowanych wzorców** (analogia: wartość z enumu u Brada, skład z zamkniętej listy w Dynamic Team), nie wymyśla nowych;
- wykonanie ma **twarde limity** (G2–G5) niezależne od tego, co agent „uważa";
- poziom autonomii wynika z kosztu i odwracalności (§8.2), a nie z pewności siebie agenta;
- wybór jest **zostawiany w śladzie** (jedna linia), żeby można było go skorygować i z czasem ocenić.

To jest wersja „dynamiczna, ale typowana", spójna z tezami źródeł, a nie swobodna improwizacja.

### 9.6 Ryzyka specyficzne dla dynamiki na poziomie setupu
1. **Rozrost reguł i dryf.** Karta wzorców to kolejny plik do utrzymania w zgodzie z resztą.
2. **Dwa narzędzia, dwie interpretacje.** Copilot i Claude Code inaczej ładują instrukcje i inaczej widzą budżet; ten sam zapis może działać różnie (nie testowałem).
3. **Brak danych o trafności doboru.** Cała ocena jest hipotezą.
4. **Przeuczenie na jednej sesji i na jednym autorze.** Wzorce w karcie warto opisać ogólnie (Anthropic, AutoGen, literatura), a nie wyłącznie terminologią runtime'u Brada.
5. **Utrata przewidywalności.** Wartością workflow jest przewidywalność; dynamikę traktować jako wyjątek z zapisanym powodem, domyślnie statyczny przepływ.

### 9.7 Drabina dojrzałości (koncepcyjna, bez kroków wdrożenia)

| Poziom | Opis | Co by to dowodziło |
|---|---|---|
| L0 | stały pipeline z bramkami (dziś) | baza |
| L1 | w planie pojawia się **doradczy** wybór kształtu z szacunkiem kosztu | czy proponowany kształt ma sens |
| L2 | **autonomia warstwowa** A/B/C (§8.2) | czy mniej pytań nie pogarsza wyników ani kosztu |
| L3 | dobór **informowany historią** (ślad decyzji + wynik) | czy kalibracja się poprawia |
| L4 | swobodne wymyślanie wzorców przez agenta | **nie zalecane** (sprzeczne z tezą o sterowaniu przepływem) |

### 9.8 Moja ocena
- **Da się to wpasować**, głównie przez **złożenie istniejących części** (rejestr, routing, polityki, pamięć, model kosztów). To mocna strona setupu i realna przewaga: warstwa decyzyjna jest nasza, czytelna i pod kontrolą.
- **Brakują trzy rzeczy:** wiedza o wzorcach w zwięzłej, tanio ładowalnej postaci; zapis „co wybrano i jak poszło"; pomiar kosztu rzeczywistego zamiast szacunku. Bez nich „samoświadomość" pozostaje sloganem.
- **Nie twierdziłbym „najlepsze" ani „najdojrzalsze".** Mocniejsze i prawdziwsze jest: „decyzyjność na poziomie setupu, z typowanym wyborem, twardymi limitami i śladem decyzji" oraz gotowość, by sprawdzić na własnych danych, czy to działa.
- **Niepewność:** n = 1, żadnego pomiaru trafności doboru, nie czytałem wszystkich agentów, nie testowałem zachowania w Copilocie.

---

## 10. Gotowość do rozważań implementacyjnych: co mamy, czego brakuje, jak weryfikować

**Uwaga użytkownika (2026-10-14):** te uwagi (ograniczenia, ryzyka, niepewności) są dobre do **weryfikacji rozwiązania** i **doboru odpowiedniej implementacji**. Pytanie: czy raport jest gotowy do rozważań implementacyjnych i czy mamy już wszystko.

**Odpowiedź asystenta:** raport jest gotowy do **rozważań i decyzji o kierunku**. Nie jest jeszcze gotowy do **samej implementacji**: nie mierzyliśmy kosztu ani trafności, nie podjęliście decyzji o zakresie, a raport nie przeszedł niezależnego przeglądu. Poniżej konkretna lista.

### 10.1 Co mamy (z dowodami w repo)
| Element | Gdzie |
|---|---|
| Katalog 13 wzorców, ich mechanizmy i znane ograniczenia (z cytatami) | `docs/research/2026-10-14-agent-patterns-bradmurry/analysis/` |
| Weryfikacja krzyżowa, sprzeczności źródeł, erratum | `analysis/01-cross-source-verification.md` |
| Self research (LLM-as-judge, reflection, caching, pamięć, pętla agenta itd.) | `docs/research/2026-10-14-llm-self-research/README.md` |
| Przykładowa debata z wynikiem i zdaniem odrębnym | `…/debate/` |
| Dopasowanie wzorców do setupu, kompromisy i guardrails G1–G11 | §2–§3 tego raportu |
| Autonomia warstwowa A/B/C i świadomość tokenów | §8 |
| Analiza „dynamicznego workflow" i brakujących elementów | §9 |
| Raport o ulepszeniu workflow (W1–W3, P1–P4) | `2026-10-14-agent-patterns-workflow-improvements.md` |

### 10.2 Czego brakuje przed implementacją (luki, nie kroki)
1. **Decyzje użytkownika.** Które elementy w ogóle brać pod uwagę (patrz §10.3). Bez tego implementacja byłaby zgadywaniem.
2. **Pomiar bazowy.** Rzeczywisty koszt kontekstu i typowych sesji w obu narzędziach (dziś tylko szacunek z `context-cost-analysis.md` z 2026-06-03).
3. **Dane o trafności.** Nie wiemy, ile BLOCKS MERGE jest trafnych ani czy dobór wzorca przez agenta byłby trafny; nie ma żadnych danych w źródłach.
4. **Zachowanie Copilota vs Claude Code.** Nie testowałem, jak każde z narzędzi ładuje instrukcje i widzi budżet tokenów.
5. **Niezależny przegląd samego raportu.** Raport napisał jeden asystent na jednej bazie dowodów, głównie jednego autora. Zgodnie z Waszą własną praktyką (`session-continuity`: walidacja w osobnej sesji, najlepiej innym modelem) powinien przejść taki przegląd, zanim posłuży za podstawę zmian.
6. **Niesprawdzone fragmenty setupu.** Nie czytałem w całości agentów `bc-switch`, `adr-generator`, `spec-writer`, `copilot-setup-maintainer`, `mcp-routing-eval.prompt.md` ani `tools/rag/queries.yaml`.
7. **Status formalny.** Jeśli zmiana dotknie architektury setupu, Wasza kultura to ADR (jak ADR-0033 dla GO gate). Nie sprawdzałem, czy ten zakres tego wymaga.
8. **Dodanie `docs/research/` do `docs/README.md`:** wymaga edycji poza ścieżkami zawsze dozwolonymi (`IMPLEMENT`).

### 10.3 Decyzje otwarte do podjęcia przez użytkownika (bez rekomendacji implementacyjnych)
| ID | Pytanie | Opcje | Co wpływa na wybór |
|---|---|---|---|
| D1 | Czy w ogóle wprowadzać wybór wzorca z kontekstu? | nie / tylko doradczo (L1) / autonomia warstwowa (L2) | wartość vs koszt tokenów i utrzymania |
| D2 | Które wzorce wchodzą w zakres początkowy? | np. tylko: sprawdzenie podziału zadań, jeden krytyk, debata na prośbę | dowody z §3 i §8.4 |
| D3 | Progi poziomów A/B/C (liczby to dziś tylko propozycje) | do uzgodnienia po pomiarze bazowym | pomiar kosztu (§10.2 pkt 2) |
| D4 | Gdzie ma żyć wiedza o wzorcach i jak tanio ją ładować | jedna zwięzła karta ładowana na próg „mały zakres" / inne | stały blok ≈ 7 110 tokenów |
| D5 | Gdzie zapisywać ślad decyzji o wzorcu | istniejący log / osobny / nigdzie | koszt utrzymania vs wartość kalibracji |
| D6 | Czy debata w researchu jest wyzwalana tylko na prośbę, czy też przy zdefiniowanych warunkach | tylko na prośbę / dodatkowo warunki | G1, koszt ≈ 0,5 mln tokenów |
| D7 | Jak formalizować blackboard przez pliki między modelami | zostaje doraźnie / spisane zasady | opis użytkownika, nie zweryfikowany w repo |
| D8 | Czy ujednolicać Copilot i Claude Code (np. 2 agenci w `.claude/agents/` vs 20 w `.github/agents/`) | tak / nie / selektywnie | jak często używacie research w Claude Code |

### 10.4 Kryteria weryfikacji dowolnej implementacji (propozycje)
Do użycia jako lista kontrolna przy ocenie każdego wariantu. Każde kryterium jest sprawdzalne, nie opiniowe.

| ID | Kryterium | Jak sprawdzić |
|---|---|---|
| V1 | **Ślad:** każda decyzja o wzorcu ma jednoliniowy zapis (wzorzec, powód, szacunek kosztu) | przegląd próbki sesji |
| V2 | **Zero narzutu dla małych zadań:** zadania „mały zakres" nie zużywają dodatkowych tokenów względem stanu bazowego | pomiar przed/po na tych samych zadaniach |
| V3 | **Limity działają:** wymuszony limit przerywa sztuczną pętlę (G2–G5) | test na zadaniu, które celowo się zapętla |
| V4 | **Poziom C nie startuje bez zgody** | test negatywny: próba uruchomienia kosztownego wzorca bez zgody |
| V5 | **Brak regresji jakości** względem stanu bazowego | zestaw zadań referencyjnych, wyniki oceniane najpierw deterministycznie (G8) |
| V6 | **Koszt na zadanie ≤ baza albo uzasadniony mierzalną korzyścią** | raport kosztu (G11) |
| V7 | **Zgodność narzędzi:** ten sam zapis daje te same decyzje w Copilocie i Claude Code (próbka) | porównanie na kilku zadaniach |
| V8 | **Niezależny przegląd** rozwiązania w osobnej sesji/modelu | sesja walidacji |
| V9 | **Odwracalność:** da się wyłączyć i wrócić do stanu bazowego bez utraty | próba wyłączenia |

### 10.5 Podsumowanie gotowości
- **Gotowe:** rozumienie wzorców i ich kosztów, mapa „co mamy / czego brak", zasady guardrails, kryteria weryfikacji, lista decyzji.
- **Niegotowe:** pomiary, decyzje użytkownika, niezależny przegląd raportu, test w obu narzędziach.
- **Najbardziej ryzykowne założenie raportu:** że dobór wzorca przez agenta będzie trafny i tańszy niż stały przepływ. Dziś to hipoteza bez danych; najtańszy sposób jej sprawdzenia to pomiar bazowy plus retrospektywna ocena kilku dawnych zadań.

> **Zobacz też:** [przegląd raportu i punkty weryfikacji kierunku (GO/ADJUST/PIVOT)](2026-10-14-agent-patterns-review-and-direction-checks.md): niezależny przegląd założeń, dwie symulacje i hipotezy H1–H9.
