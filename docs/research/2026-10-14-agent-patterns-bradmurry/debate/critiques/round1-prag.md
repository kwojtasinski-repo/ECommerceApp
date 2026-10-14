# Debater: prag, Round 1, critique

Skróty: 00 = analysis/00-summary.md, 01 = analysis/01-cross-source-verification.md, LSR = 2026-10-14-llm-self-research/README.md. Otwarte i sprawdzone: 00, 01, LSR (sekcje 2, 3, 6, 7, 9, 12) oraz kod repo.

## Krytyka PRO

**P1. pro#1 "Kierunek potwierdzają niezależnie Zheng 2023 i Huang 2023" jako poparcie reducera.** Przesunięcie: Zheng dotyczy biasów sędziego, Huang samokorekty. Żadne nie dotyczy "LLM jako maszyny stanów". 01 §4 D5 ocenia to jako "Partially supported" i "zgodne... na poziomie kierunku (nie dosłownie)". Pro zaznacza to w ocenie, ale używa tych prac jako uzasadnienia dla kosztownego mechanizmu. Dla 1-3 dev wystarczy enum + `switch` w C#, bez żadnego reducera. Werdykt: nadinterpretacja, kierunek wspierany.

**P2. pro#5 "Repo ma już Outbox/Inbox i sagi, więc to zwykła kontynuacja".** Sprawdziłem repo: Outbox/Inbox istnieją (`Messaging/IOutboxWriter`, `OutboxCleanupTask`, `InboxCleanupTask`, `IProcessedMessageGuard`), sagi też (`Application/Sagas/ISagaDefinition`, `RefundSagaDefinition`, `CartRecoverySagaDefinition`). Natomiast grep po `Anthropic|OpenAI|Chatbot|IChatClient|SemanticKernel` w `*.cs`/`*.csproj` nie zwrócił nic. To jest deterministyczna powłoka dla zdarzeń domenowych, nie dla wywołań LLM. Brakuje klucza idempotencji dla tury czatu, dziennika przebiegu agenta, limitu iteracji. Dlatego teza "mamy już" jest trafna tylko w sensie dyscypliny i gotowych klocków (idempotentny konsument, DLQ), nie gotowej powłoki dla agenta. Dotyczy to też mojej pozycji prag ("to już macie") i tu **częściowo konceduję**: sformułowanie było za mocne. Poprawka: dla czatbota tylko-odczytowego powłoka nie jest potrzebna, a dla akcji z efektem (zwrot) wypada przepiąć istniejącą `RefundSaga` zamiast budować nowy journal.

**P3. pro#4 "STRONG" dla Generator/Critic/Refiner (wzorzec 9).** Co jest STRONG, to zasada "zewnętrzny sygnał". Sam wzorzec 9 Brada ma ograniczenie (refiner re-emituje cały artefakt, 00 §B). A dla czatbota sprawdzenie SKU/ceny to zwykły post-walidator, nie pętla Critic/Refiner. Etykieta wzorca jest tu zbędna i podnosi pozorny koszt wdrożenia. Pro#4 pomija też, że Reflexion 91% to deklaracja autorów (sam pro to wskazuje), więc "STRONG" jest zawyżone dla całości.

**P4. pro#3 router jako enum "STRONG co do wzorca".** Poparcie to Anthropic routing [LSR#1,#5], zgoda. Ale LSR §12 sam zastrzega, że sekcje 1 i 5 częściowo opierają się na snippetach i że nie było krzyżowej weryfikacji. Wzorzec 3 Brada zaś ma udokumentowany błąd ("trasowanie dwa razy... does nothing", 00 §B), czyli kopiowanie jego wariantu bez jego runtime'u nie ma sensu. Do wdrożenia: router w kodzie, bez referencji do wzorca 3.

## Krytyka CON

**C1. con#1 "W repo mamy już Outbox/Inbox i saga engine, więc deterministyczna powłoka jest naszym własnym kodem" (jako argument przeciw).** Ten sam błąd co u mnie, patrz P2. Poprawnie: to dyscyplina i klocki, a nie rozwiązanie dla pętli LLM. Con ma rację, że nie wolno importować Aegis. Natomiast wniosek "nic nie budujemy" nie wynika, bo limity iteracji, idempotentny klucz tury i log przebiegu agenta wciąż trzeba dodać (01 C1: guard nie obejmuje tur).

**C2. con#3 autokorekta autora (C1) jako dowód słabości.** Sprawdzone w 01: C1 jest realne (intro "bounded by... no-progress guard" vs "does not span turns"). Poparte. Ale con używa tego przeciw wzorcowi w ogóle, a dla czatbota wniosek jest przeciwny: skoro guard nie obejmuje tur, limit tur trzeba mieć w kodzie ASP.NET. To argument za wdrożeniem limitu, nie przeciw. Wniosek con: nadmiernie ogólny.

**C3. con#6 AutoGen C9-C11.** Poparte w 01 (C7-C12 rzeczywiście rozbieżności proza/kod, C10 cytat o "not meant to be used in real applications"). Niedopowiedzenie: con sam pisze "MEDIUM", ale pomija, że 01 §5 ostrzega o 5,2% niezgodnych cytatów w findings, więc dosłowność tych cytatów też jest "do sprawdzenia". Nie zmienia wniosku (SKIP), ale argument nie jest bardziej pewny niż reszta.

**C4. con#7 "Dowód ryzyka: STRONG (OWASP)" dla pamięci.** Misattrybucja: LSR §7 podaje to jako "Ryzyka (moja ocena)", a OWASP LLM04 dotyczy zatrucia danych/modelu, nie bezpośrednio pamięci rozmów. Con zaznacza "moja ocena, nie źródło", ale potem etykietuje to STRONG. Właściwie: ryzyko prawdopodobne (RODO), dowód MEDIUM. Wniosek DEFER pozostaje słuszny.

**C5. con#8 "bez zbioru ewaluacyjnego nie ma czego optymalizować".** Zgoda, ale con nie podaje kryterium "kiedy". Brak progów awansu to luka praktyczna (por. moja sekcja c).

## Punkty zgodności (kandydaci na konsensus)
1. Pierwszy slice: proxy, klucz po stronie serwera, `max_tokens`, budżet kosztowy, prompt caching (pro#6/7, con "co przyznaję", prag ADOPT).
2. Limity pętli poza modelem oraz zewnętrzny, deterministyczny sygnał zamiast LLM-judge (pro#2/#4, con#5, prag).
3. Brak Aegis jako zależności, brak debaty/quorum/pamięci długoterminowej/auto-ewolucji na starcie (pro "Czego nie bronię", con, prag SKIP/DEFER).

## Wciąż sporne
1. Czy wzorce Brada (3, 8, 9) są potrzebne jako nazwane wzorce, czy wystarczy zwykły C# (pro: tak, jako słownik; con i prag: kod bez etykiet).
2. Czy akcje z efektem (zwrot) wolno kiedykolwiek oddać agentowi: pro dopuszcza po idempotencji/journalu, con wyklucza, prag: tylko przez istniejącą saga/endpoint, po pilotażu odczytowym.
