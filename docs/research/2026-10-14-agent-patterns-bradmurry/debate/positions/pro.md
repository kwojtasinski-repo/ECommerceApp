# Debater: pro, Round 1, position

Skala: STRONG = źródło pierwotne/niezależne (kilka grup); MEDIUM = jedno jawne źródło; WEAK = samoopis jednego autora.
Cytowanie: [plik#sekcja]. Skróty: 00 = analysis/00-summary.md, 01 = analysis/01-cross-source-verification.md, LSR = 2026-10-14-llm-self-research/README.md.

## Teza
Warto wdrożyć **deterministyczną powłokę wokół probabilistycznego modelu** jako zasadę nadrzędną. Zasada ta jest zgodna z niezależną literaturą. Nie trzeba wdrażać runtime'u "Aegis" Brada: jego idee da się zaimplementować w .NET zwykłym kodem, a jego samego nie sprawdzono niezależnie.

## Twierdzenia

1. **Model wnosi wartość, a kontrola siedzi poza modelem (reducer: stan = typowany kontekst, legalność strukturalna, nie "prośba o przejście").** Zapobiega "LLM jako maszyna stanów", czyli dryfowi kroków i przeskokom przez walidację (np. potwierdzenie zamówienia bez weryfikacji). Kierunek potwierdzają niezależnie Zheng 2023 i Huang 2023 (LLM nie koryguje się sam bez zewnętrznego sygnału) oraz Anthropic ("predefined code paths") [01#2; LSR#1,#3]. **STRONG co do kierunku.** Sam runtime reducera to **MEDIUM/WEAK**: jeden autor, "the glue code is gone" uznano za Unsupported [01#4 D1].

2. **Limity pętli poza modelem (maxIterations, cap 128 mikrokroków, budżet `size(drafts)<4`, stan `stalled`).** Zapobiega nieskończonym pętlom i niekontrolowanemu kosztowi. Anthropic: "Stopping conditions, such as a maximum iteration count" [LSR#8]. **STRONG.** Zastrzeżenie wzięte z korekty autora: no-progress guard nie obejmuje tur, więc limit międzyturowy trzeba dodać samemu [01#1 C1].

3. **Wzorce 3 (Supervisor/Router, enum `route`) i 8 (Planner/Executor) jako klasyfikator intencji.** Wyjście modelu to enum o skończonej liczbie wartości (FAQ / status zamówienia / zwrot / człowiek), a dalszy przepływ jest w kodzie. Zapobiega wolnotekstowym "decyzjom" modelu. Zgodne z wzorcem *routing* Anthropic [LSR#1,#5]. **STRONG co do wzorca, MEDIUM co do implementacji Brada.**

4. **Wzorzec 9 (Generator/Critic/Refiner) tylko z zewnętrznym sygnałem.** Krytykiem ma być deterministyczny check: czy SKU istnieje w katalogu, czy URL zwraca 200, czy cena zgadza się z bazą. "LLM-as-judge" służy wyłącznie jako sygnał pomocniczy. To zapobiega halucynowanym produktom i cenom (LLM09 Misinformation). Zheng, Huang i Brad zgadzają się, że sędzia jest słaby (position/verbosity/self-enhancement bias) [01#2]. **STRONG.** Reflexion (91% HumanEval) to deklaracja autorów [LSR#3].

5. **Idempotencja, journal, replay, "timeout is UNKNOWN, not FAILED" (Agents in Production).** Repo ma już Outbox/Inbox i sagi, więc to zwykła kontynuacja istniejącej dyscypliny. Dotyczy akcji z skutkiem (zwrot, anulowanie): klucz idempotencji, zapis przebiegu, brak automatycznego ponowienia przy nieznanym wyniku. Zapobiega podwójnym zwrotom i zdublowanym zamówieniom. Sam wzorzec jest klasyczny w systemach rozproszonych. Przeniesienie go na agenta opisuje tylko Brad: **WEAK/MEDIUM** [00#A].

6. **Taniość bez zmiany jakości: prompt caching + routing do SLM.** Dokumentacja Anthropic: odczyt 0,1x, zapis 1,25x (TTL 5 min) lub 2x (1 h), wymagany identyczny prefiks, zmiana narzędzi unieważnia cache. Prefiks sklepu (system prompt, FAQ, polityki) jest idealny na cache [LSR#6]. **STRONG** (oficjalna dokumentacja). SLM: position paper NVIDIA, autorzy sami nazywają to "value statement" [01#4 D6], więc **MEDIUM**. Liczby "10-30x taniej" z blogów pomijam.

7. **Zasady bezpieczeństwa czatbota: klucz tylko na serwerze, brak uprawnień do zmian, budżet kosztowy, OWASP LLM01/06/10.** Zapobiega prompt injection i nadmiernej sprawczości. Akcje tylko przez istniejące, autoryzowane endpointy [LSR#9]. **STRONG** (OWASP) dla listy ryzyk. Szczegóły z blogów to **WEAK**.

8. **Pętla self-evolving (RUN/MEASURE/DISCOVER/BUILD) i optymalizacja promptów (GEPA/DSPy).** Zasady "jedna zmiana naraz, zgłoś regresję" są tanie i zdrowe. Metryki Brada (np. 612 400 -> 26 400 tokenów) to samoopis bez prób [00#Gaps]. GEPA to deklaracja autorów, niezweryfikowana niezależnie [LSR#4]. **WEAK/MEDIUM.** Dlatego: tylko jako późny krok, po zbudowaniu zbioru ewaluacyjnego.

## Czego nie bronię
Debaty wielu agentów i quorum: źródła nie zawierają żadnych danych o skuteczności [01#2, ocena Low]. AutoGen group chat sam deklaruje "not meant to be used in real applications" [01#1 C10]. Pamięć długoterminowa (Mem0, MemGPT): deklaracje autorów produktu i ryzyko RODO [LSR#7]. Jedna część "Aegis" (Event-Driven) jest "half real" [00#B].

## Kolejność wdrożenia
1. **Szkielet bezpieczeństwa i kosztu (tydzień 1):** backend-proxy, `max_tokens` i budżet per użytkownik, brak narzędzi zapisujących, prompt caching prefiksu, pomiar `cache_read_input_tokens`. Pierwszy slice: czatbot FAQ/polityki bez żadnych akcji.
2. **Deterministyczna powłoka (tygodnie 2-3):** router intencji jako enum (wzorzec 3), limit iteracji, deterministyczne sprawdzenie SKU/URL/ceny przed wysłaniem odpowiedzi (wzorzec 9 z zewnętrznym sygnałem), dziennik przebiegu. Akcje tylko odczytowe (status zamówienia przez autoryzowany endpoint).
3. **Ewaluacja i optymalizacja (później):** zbiór pytań testowych, LLM-judge skalibrowany na etykietach ludzkich, dopiero potem SLM do routingu i ewentualnie GEPA. Akcje z efektem (zwroty) dopiero z idempotencją i journalem (twierdzenie 5).
