# Przegląd raportu o wzorcach agentowych i punkty weryfikacji kierunku

- **Data:** 2026-10-14
- **Czego dotyczy:** przegląd [raportu o dopasowaniu wzorców](2026-10-14-agent-patterns-fit-tradeoffs-guardrails.md) (§3, §8, §9, §10) i [decyzji debaty](../research/2026-10-14-agent-patterns-bradmurry/debate/decision.md) oraz **punkty, które powiedzą, czy kierunek jest dobry, czy trzeba zrobić coś innego** (§6).
- **Zakres przeglądu (celowo nie pełny):** niezależny przegląd założeń względem kodu + dwie symulacje deterministyczne na historii repo i kodzie. **Nie zrobiłem** symulacji doboru wzorca przez agenta, testu A/B debaty ani testu w Copilocie.
- **Ograniczenie niezależności:** przeglądający agent to ten sam typ modelu co autor raportu, tylko w osobnym kontekście. To redukuje, ale nie usuwa wspólnego biasu.
- **Status:** wyniki symulacji to dane z 200 commitów i z grepów regexami (przybliżenia). Wnioski to oceny asystenta.

---

## 1. Czy tego nie wyjaśni agent implementujący, który bierze raport i planuje?

**Częściowo tak, i ten przegląd to potwierdza.** Agent `assumption-check` sprawdził założenia raportu względem repo w osobnym kontekście: ok. 63 s, 34 wywołań narzędzi, ≈ 66,7 tys. tokenów. Znalazł istotny błąd (patrz §3, SKU), którego sam raport nie wykrył. Plan zbudowany na surowym raporcie odziedziczyłby ten błąd.

**Czego agent planujący nie wyjaśni:** decyzji strategicznych (D1–D8 w raporcie §10.3), pomiaru bazowego (koszt tokenów) ani tego, czy sama hipoteza „dobór wzorca z kontekstu się opłaca" jest prawdziwa. Plan będzie budowany **na** twierdzeniach raportu, także błędnych.

**Wniosek:** rozsądna kolejność to taka, którą już zrobiliśmy: tani przegląd założeń przed planowaniem. Planista i tak powinien go powtórzyć dla konkretnej zmiany.

---

## 2. Co zrobiłem w przeglądzie

| Element | Co | Koszt |
|---|---|---|
| R1 | Niezależny przegląd założeń repozytoryjnych (`assumption-check`) | ≈ 66,7 tys. tokenów |
| S1 | Symulacja: rozkład wielkości zadań z historii git (200 commitów) | skrypt, bez LLM |
| S2 | Symulacja: skan kodu pod kątem naruszeń reguł BLOCKS MERGE możliwych do sprawdzenia mechanicznie | skrypt, bez LLM |
| Nie zrobione | symulacja doboru wzorca przez agenta (tryb cienia), A/B debata vs jeden krytyk, test Copilot vs Claude Code, chatbot w praktyce | — |

*Uwaga techniczna:* wynik subagenta został przez zabezpieczenia środowiska oznaczony jako dopasowany do wzorca „instrukcji" (dotyczącego pliku ustawień). Potraktowałem go wyłącznie jako dane; nic z niego nie wykonywałem. Treść dotyczyła zwykłego sprawdzenia faktów.

---

## 3. Wyniki przeglądu założeń (R1)

Dowód to pliki w repo (ścieżki podane przez agenta). Oceny: POPRAWNE / CZĘŚCIOWO / NIEPOPRAWNE / NIEZWERYFIKOWANE.

| Założenie raportu | Ocena | Istotne szczegóły |
|---|---|---|
| ArchUnitNET używany tylko w `BoundedContextDependencyTests` | **POPRAWNE** | jeden plik w `ECommerceApp.UnitTests/Architecture/`; brak analizatorów Roslyn, `.editorconfig`, `Directory.Build.props` |
| „Większość BLOCKS MERGE nie jest egzekwowana deterministycznie" | **CZĘŚCIOWO** | kierunek zgodny, ale to szacunek; **moja symulacja S2 (§5) go osłabia** |
| Brak integracji LLM w kodzie sklepu | **POPRAWNE** (z nuansem) | jedyne trafienie: `tools/rag-dotnet` (Ollama do embeddingów), narzędzie deweloperskie poza aplikacją |
| Autoryzacja zamówień i gości istnieje (`OrderAccessAuthorizationHandler`, `VerificationCode*`, `CustomerOrGuestPolicy`) | **POPRAWNE** | **ale** to kontrolery MVC z cookie/claims, a nie API pod proxy czatbota; czy da się wywołać z backendowego proxy, nie sprawdzono |
| Czatbot „tylko odczyt przez autoryzowane ścieżki" | **CZĘŚCIOWO** | raport pomija, że czatbot musiałby działać jako zalogowany klient albo gość z zakresem jednego zamówienia (`GuestAccessScope`) |
| **Walidator „SKU, URL, cena"** (decision.md §a, §b pkt 7) | **NIEPOPRAWNE** | `Product.cs`: jest `Name` i `Cost` (cena); **brak SKU i brak pola URL**; adres produktu to trasa `{id:int}` w `StorefrontController`, czyli URL jest pochodną Id |
| 20 agentów w `.github/agents`, 2 w `.claude/agents` | **POPRAWNE** | |
| Liczby w `setup-state.md` (instrukcje 21, prompty 17, agenci 20, skille 44, ADR 33, konteksty 8) | **POPRAWNE** | instrukcji jest 22 pliki, w tym `docs-index.full.md` (prawdopodobny wyjątek konwencji) |
| Plany w `.github/plans/` ignorowane przez git | **POPRAWNE** | `.gitignore:391-394` |
| `agent-decisions.md`: reguła promocji 2+ | **POPRAWNE** | linia 23; plik ma 362 linie (zweryfikowane osobno, `wc -l`) |
| Hooki `UserPromptSubmit` i `PreToolUse` | **POPRAWNE** | zweryfikowane osobno w `.claude/settings.json` (agent tego nie sprawdził) |

**Błąd do naprawy:** walidator czatbota musi opierać się na **Id produktu, trasie i cenie (`Cost`)**, nie na SKU/URL. Erratum dopisane w `debate/decision.md`.

---

## 4. Symulacja S1: jakiej wielkości są nasze zadania (git, 200 commitów)

Okres: 2026-05-27 do 2026-10-14. Podział (progi **moje**): S = ≤ 2 pliki i ≤ 60 linii; L = > 12 plików, albo ≥ 4 projekty, albo > 800 linii; reszta M.

| Klasa | Liczba | Udział |
|---|---|---|
| S (małe) | 21 | 10% |
| M (średnie) | 95 | 48% |
| L (duże) | 84 | 42% |

Mediany: **7 plików i ≈ 300 linii** na commit. Wrażliwość na progi: ≤ 3 plików 30%, ≤ 5 plików 41%, ≤ 100 linii 24%, ≥ 10 plików 39%, ≥ 20 plików 16%. Typy: `feat` 36 (z tego 27 dużych), `fix` 19, `test` 30, `docs` 28, `other` 70 (komunikaty bez konwencji).

**Zastrzeżenia (ważne):** commit to nie zadanie. Wasza praktyka faz i wsadów zbiera wiele zmian w jednym commicie, co **zawyża** rozmiar. Progi są arbitralne. Do próbki wchodzą też dokumenty i testy.

**Wniosek:** moje wcześniejsze „większość zadań jest mała, więc narzut wyboru wzorca musi być zerowy" **nie jest poparte** na poziomie commitów. Małych jest ok. 10–30% zależnie od progu; **dominują średnie i duże**. Zero narzutu dla małych zadań nadal jest potrzebne (Twoja uwaga o poprawce formularza), ale to **nie jest typowy przypadek**; typowy przypadek to praca średnia i duża, gdzie wybór wzorca (fan-out, niezależna walidacja, fazy) ma realną szansę się opłacić **i** realnie kosztuje tokeny.

---

## 5. Symulacja S2: czy reguły BLOCKS MERGE są łamane w kodzie

Skan `git grep` (tylko pliki śledzone) pod kątem reguł z `anti-patterns-critical.context.md`, które da się sprawdzić regexem. Test pozytywny działania grepa: 471 trafień dla `Task<` w `Application`.

| Reguła | Trafienia | Uwagi |
|---|---|---|
| `.Result` / `.Wait()` / `GetAwaiter().GetResult()` w kodzie produkcyjnym | 0 | |
| `catch` w kontrolerach | 0 | |
| `using ECommerceApp.Infrastructure` w `Application` | 0 | |
| `IQueryable<` w `Application` | 0 | |
| publiczne settery w `Domain/Sales` | 0 | |
| zahardkodowane `Roles = "..."` | 0 | |
| goły `Assert.` w testach jednostkowych / integracyjnych | 0 / 0 | `.Should()` użyte 1 745 razy |
| `User.Claims` / `HttpContext.User` w kontrolerach | 4 | wszystkie w `BaseController` (miejsce dozwolone przez regułę) |
| **Rejestracje usług bezpośrednio w `Startup.cs`** (reguła: używać `DependencyInjection.cs`) | **8 linii** (Web 6, API 2) | `AddScoped/AddSingleton` dla usług aplikacji, np. `IShopperIdentityResolver`, `IOrderAccessAuthorizer`, `MaxApiQuantityFilter`. Albo tolerowany wyjątek warstwy Web/API, albo niewychwycone naruszenie. **Nie rozstrzygnąłem.** |

**Zastrzeżenia:** regexy są przybliżone (możliwe fałszywe negatywy); nie oddzieliłem kodu legacy od nowego; test pozytywny tylko dla jednego wzorca.

**Wniosek, który zmienia moją wcześniejszą ocenę:** dla **9 z 10 sprawdzanych reguł naruszeń nie ma**, więc obecny przegląd i dyscyplina działają dobrze. Deterministyczna bramka dla tych reguł **dziś niewiele by wyłapała**; jej wartością byłaby ochrona przed przyszłym dryfem. Dlatego **obniżam** swoją wcześniejszą pozycję nr 1 („deterministyczny rdzeń dla BLOCKS MERGE") do „może później". Jedyna reguła z rozbieżnością (rejestracja w `Startup.cs`) to pytanie do Was: wyjątek czy luka?

---

## 6. Punkty weryfikacji kierunku: czy to dobry kierunek, czy lepiej zrobić coś innego

**Kierunek oceniany:** *dynamiczny dobór wzorców i poziomu autonomii na poziomie setupu (typowany, z limitami i śladem decyzji), plus bramki dowodowe w researchu.*
Progi i liczby poniżej to **propozycje do uzgodnienia**. Stan „dziś" wynika z tego przeglądu.

| ID | Hipoteza | Test (tani, tam gdzie się da deterministyczny) | Warunek „PASS" (propozycja) | Stan dziś | Jeśli „FAIL": inna droga |
|---|---|---|---|---|---|
| H1 | Dobór wzorca z kontekstu daje wartość, bo zadania często są średnie/duże | ręczne oznaczenie 20 ostatnich zadań: czy dałoby się wskazać wzorzec z wiarygodną korzyścią | ≥ połowa zadań M/L ma nazwalny wzorzec z korzyścią | **częściowo poparte** (S1: 90% M/L na poziomie commitów; caveat: commit ≠ zadanie) | nie budować warstwy doboru; zostawić statyczny pipeline |
| H2 | Deterministyczne checki reguł BLOCKS MERGE wyłapią to, co przegląd przeoczy | skan S2 (zrobiony) | ≥ 1 reguła z realnymi naruszeniami poza wyjątkami | **niepoparte** (9/10 reguł bez naruszeń; 1 niejednoznaczna) | pominąć bramki dla tych reguł; inwestować tylko tam, gdzie luka jest zmierzona |
| H3 | Weryfikacja kompletności źródeł i cytatów w researchu poprawia wiarygodność | kolejne 3 researche z bramką | w każdym ≥ 1 realny brak/błąd wychwycony, którego sam LLM by nie zgłosił | **poparte** (5/6 odpowiedzi fragmentarycznych; 94,8% cytatów zgodnych) | porzucić |
| H4 | Debata na prośbę poprawia wynik researchu ponad pojedynczego krytyka | A/B na 3 syntezach: krytyk + checki vs debata; liczyć **zweryfikowane** korekty na 100 tys. tokenów | debata znajduje ≥ 1 dodatkową zweryfikowaną korektę w ≥ 2 z 3 przypadków przy kosztowi ≤ 3× | **niesprawdzone** (jedna debata: 5 z 7 opisanych twierdzeń błędnych lub przesadzonych; brak grupy kontrolnej) | wyłącznie pojedynczy krytyk + checki |
| H5 | Agent trafnie dobiera wzorzec i poziom autonomii | tryb cienia: agent zapisuje propozycję (wzorzec, poziom, koszt) dla 20 zadań bez wykonywania; człowiek oznacza zgodność | zgodność ≥ 80% i zero propozycji wzorca dla zadań S | **niesprawdzone** | zostać na poziomie doradczym (L1) albo statycznym |
| H6 | Karta wzorców nie zjada tokenów | pomiar `/context` przed i po; karta ładowana tylko powyżej progu | dla zadań S dodatkowe tokeny = 0; dla M/L narzut ≤ kilka % stałego bloku | **niesprawdzone** (szacunek ≈ 500 tokenów, moja propozycja) | karta zewnętrzna ładowana ręcznie na żądanie |
| H7 | Czatbot tylko do odczytu jest wykonalny w tej architekturze | krótki spike: czy backendowy komponent pobierze status zamówienia w zakresie gościa/klienta bez przepływu cookie | tak, z istniejącą autoryzacją | **częściowo** (autoryzacja istnieje, ale w MVC/cookie; SKU nie istnieje) | czatbot tylko z RAG (FAQ/polityki) + przekazanie człowiekowi dla zamówień |
| H8 | Copilot i Claude Code zachowują się spójnie przy tych samych zapisach | 3 te same zadania w obu narzędziach | te same decyzje w ≥ 2 z 3 | **niesprawdzone** | warianty per narzędzie albo ograniczenie do jednego |
| H9 | Kierunek nie pogarsza jakości ani nie podnosi kosztu | zestaw zadań referencyjnych, pomiar przed/po | brak spadku jakości, koszt ≤ baza lub uzasadniony | **niesprawdzone** | powrót do stanu bazowego (L0) |

### 6.1 Reguła decyzji o kierunku (propozycja)

| Wynik | Znaczenie | Co z tym |
|---|---|---|
| **GO** | H1, H5 i H6 PASS, a H9 bez regresji | rozwijać dobór wzorców (L1 → L2) |
| **ADJUST** | H1 PASS, ale H5 lub H6 FAIL | zostać przy doradczym doborze lub ręcznej karcie |
| **PIVOT** | H1 FAIL albo H9 FAIL | nie budować warstwy doboru; zostawić tylko bramki dowodowe (H3) i pomiar |
| **Bez zmian** | H4 FAIL | debata tylko jako jednorazowe, świadome użycie, bez rozwijania |

### 6.2 Stan kierunku dziś (na podstawie tego przeglądu)
- **Poparte:** bramki dowodowe w researchu (H3). To jedyna część ze zmierzonym dowodem.
- **Częściowo:** potrzeba doboru wzorców (H1), wykonalność czatbota (H7).
- **Niepoparte:** deterministyczne bramki BLOCKS MERGE (H2).
- **Niesprawdzone:** wartość debaty ponad jednego krytyka (H4), trafność doboru (H5), koszt karty (H6), spójność narzędzi (H8), brak regresji (H9).
- **Ogólny stan: ADJUST.** Kierunek jest rozsądny, ale tylko jedna jego część ma zmierzony dowód. Warstwa „dynamicznego doboru" jest hipotezą. Najtańsze kolejne dane to H1 (oznaczenie 20 zadań), H6 (pomiar) i H7 (spike), a H3 przy kolejnym researchu.

---

## 7. Co zmieniło się w mojej ocenie po przeglądzie
1. **Obniżam** priorytet deterministycznych bramek BLOCKS MERGE (S2: wysoka zgodność z regułami).
2. **Korygują się** moje założenia o rozkładzie zadań: małe zadania to ok. 10–30%, nie większość (S1).
3. **Błąd w decyzji o czatbocie:** nie ma SKU ani URL; walidator ma sprawdzać Id, trasę i cenę (R1). Dodatkowo czatbot działa jako klient lub gość z zakresem jednego zamówienia (R1).
4. **Potwierdza się Twoja uwaga:** tani przegląd założeń przez agenta (R1) wyłapał realny błąd w kilka minut, więc agent planujący może wiele wyjaśnić, o ile najpierw sprawdzi założenia.
5. **Nadal niepewne:** wartość debaty ponad jednego krytyka i trafność doboru wzorca. Tego żaden z moich testów nie rozstrzyga.

## 8. Ograniczenia tego przeglądu
- n = 1 sesja; jeden autor i jeden przeglądający z tej samej rodziny modeli.
- 200 commitów to proxy zadań; regexy S2 to przybliżenia bez rozdziału na legacy.
- Przeglądający agent nie sprawdził m.in. hooków, liczby linii `agent-decisions.md` (sprawdziłem ręcznie: 362) ani tego, czy proxy czatbota może wywołać istniejące endpointy.
- Nie zrobiłem symulacji LLM doboru wzorca (H5), bo kosztuje to realne tokeny i wymaga Twoich ocen zgodności.

---

## 9. Aktualizacja 2026-10-15: sprawdzenie hipotez niepoprawnych i niesprawdzonych

Szczegóły, źródła i skrypty: [`docs/research/2026-10-15-pattern-selection/README.md`](../research/2026-10-15-pattern-selection/README.md). Skrót zmian stanu hipotez z §6:

| ID | Stan 2026-10-14 | Stan 2026-10-15 | Powód |
|---|---|---|---|
| H1 | częściowo poparte | częściowo poparte | symulacja reguły: 34% commitów bez wzorca, brak rekomendacji dla małych zadań; brakuje oznaczeń człowieka (arkusz 20 wierszy gotowy) |
| H2 | niepoparte | **częściowo poparte** (skala mała) | rozszerzony skan: 8 linii rejestracji w `Startup.cs` i bezpośrednie `fetch` w 2 plikach JS; nie wiadomo, czy to tolerowane wyjątki |
| H3 | poparte | poparte | bez zmian |
| H4 | niesprawdzone | **literatura nie wspiera debaty jako domyślnego wyboru** | Zhang i in. 2025: debata często nie przewyższa prostych baz; pomaga heterogeniczność modeli; nasz wynik dotyczy krytyki syntezy, nie benchmarków |
| H5 | niesprawdzone | niesprawdzone | reguła policzona i odporna (90% zgodności przy zmianie progów), ale trafność wymaga etykiet |
| H6 | niesprawdzone | **zmierzone na szkicu: ≈ 455 tokenów (387–524)** | ok. 6,4% stałego bloku; uzasadnia ładowanie na żądanie |
| H7 | częściowo | **lepiej poparte** | `IOrderAccessService` (Application) dostępny po tokenie bez `ClaimsPrincipal`; end-to-end niesprawdzone |
| H8 | niesprawdzone | niesprawdzone | wymaga uruchomienia obu narzędzi |
| H9 | niesprawdzone | niesprawdzone | wymaga zestawu referencyjnego |

**Stan kierunku nadal: ADJUST.** Warto zauważyć dwie rzeczy: (1) dobór wzorca jest **obliczalny jako reguła z cech**, ale jego jakość zależy głównie od definicji cech (kalibracja zmniejszyła „niezależną walidację" z 42% do 20% commitów jedną zmianą proxy ryzyka); (2) dowody z literatury **zalecają ostrożność wobec debaty** i wymóg różnych modeli.
