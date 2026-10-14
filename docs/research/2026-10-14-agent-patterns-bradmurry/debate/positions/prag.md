# Debater: prag, Round 1, position

Perspektywa: zespół 1-3 dev., istniejąca aplikacja .NET (Outbox/Inbox, saga, przepływy zamówień/płatności). Ranking = (wartość x siła dowodów) / (koszt x ryzyko). Skala dowodów: STRONG = niezależne/pierwotne, MEDIUM = jedno jawne źródło, WEAK = samoopis jednego autora.

## (a) Tiery

**ADOPT NOW**
- **Proxy backendowe + klucz tylko na serwerze + limity (żądania i budżet kosztowy, `max_tokens`)** - OWASP LLM10 Unbounded Consumption. STRONG [2026-10-14-llm-self-research/README.md#9].
- **Zasada "najprostsze najpierw": workflow zamiast agenta** - Anthropic i Brad zgodnie: "a for-loop could do for free". STRONG [01-cross-source-verification.md#2].
- **Brak uprawnień zapisu do zamówień (LLM06 Excessive Agency)**; status zamówienia tylko przez istniejące, autoryzowane endpointy. STRONG [README#9].
- **Jawny limit pętli poza modelem** (max iteracji, budżet tokenów) - zgodne: Anthropic, ReAct, Brad. STRONG kierunkowo [01#2].
- **Prompt caching stałego prefiksu** (system prompt + FAQ + polityki): odczyt 0.1x ceny wejścia, zapis 1.25x. STRONG (dokumentacja dostawców, ceny z 2026-10-14) [README#6].
- **RAG po katalogu/FAQ/zwrotach + deterministyczne checki** (czy SKU/URL istnieje). MEDIUM [README#9].
- **Deterministyczna powłoka wokół modelu** (idempotency, journal, timeout = UNKNOWN, DLQ): to już macie w Outbox/Inbox/sadze - wzorzec Brada jest tu tylko potwierdzeniem, nie nowym kosztem. MEDIUM [00-summary.md#A].

**PILOT**
- **LLM-as-judge skalibrowany na etykietach ludzkich** - Zheng: ">80% zgodności z ludźmi", ale position/verbosity/self-enhancement bias. STRONG co do biasów, MEDIUM co do praktyk [README#2].
- **Routing do małego modelu (SLM/tańszy tier)** dla FAQ/klasyfikacji intencji - Anthropic routing; sam paper SLM to position paper. MEDIUM-WEAK [README#5].
- **Reflection tylko z zewnętrznym sygnałem** (np. walidator linków/SKU) - Huang: bez feedbacku bywa gorzej. STRONG ostrzeżenie [README#3].
- **Krótka pamięć sesji** (bez długoterminowej, bez danych osobowych).

**DEFER**
- **Pamięć długoterminowa użytkownika** (Mem0/MemGPT) - RODO, zatrucie pamięci; liczby Mem0 to paper autorów produktu. WEAK-MEDIUM [README#7].
- **Automatyczna optymalizacja promptów (GEPA/DSPy)** - deklaracje autorów, brak niezależnej weryfikacji; najpierw potrzebny zbiór ewaluacyjny. MEDIUM [README#4].
- **Własny hosting SLM / fine-tuning LoRA** - koszt utrzymania przy małym ruchu nie ma uzasadnienia. WEAK.

**SKIP**
- **Debata multi-agent, quorum, aukcje, dynamiczne zespoły (wzorce 6, 10, 12, 13)** - "Low (brak dowodu skuteczności)" [01#2]; koszt = debaterzy x rundy; czatbot sklepu tego nie potrzebuje.
- **Runtime reactive reducer (Aegis) jako zależność** - jeden autor, brak publicznej, zweryfikowanej implementacji [00-summary.md#Gaps]; metryki baseline->optimum to WEAK (niespójność C5).
- **Self-evolving loop na produkcji** - patrz wyżej; Mastra/Medium bez treści, nie wchodzą w ogóle.
- **Wzorce AutoGen jako kod produkcyjny** - sama strona: "not meant to be used in real applications" (C10), liczne rozbieżności proza/kod (C7-C12).

## (b) Minimalna architektura czatbota

1. Widget (JS, SSE) -> endpoint ASP.NET (proxy). Klucz i prompty tylko po stronie serwera.
2. Limity: per IP/sesja/dzień, dzienny budżet kosztowy z kill-switchem, `max_tokens`, limit tur.
3. RAG: indeks katalogu, FAQ, polityk; treść pobrana = niezaufana (injection pośrednia); zakładać wyciek system promptu.
4. Prompt: stały prefiks (cache), zmienne na końcu; monitorować `cache_read_input_tokens`.
5. Dostęp do danych: wyłącznie odczyt przez istniejące, autoryzowane zapytania (status zamówienia po weryfikacji użytkownika); zero komend zapisu. Eskalacja do człowieka jako stan końcowy.
6. Logowanie rozmów (bez nadmiarowych danych osobowych), wsparcie kasowania.
7. Ewaluacja: zestaw pytań (start ~50-100 z realnych zgłoszeń - to moja propozycja, nie z źródeł), checki deterministyczne, sędzia jako sygnał pomocniczy, kalibrowany na etykietach ludzkich; zamiana kolejności odpowiedzi; sędzia z innej rodziny niż generator (wniosek, nie cytat) [README#2].

## (c) Kryteria awansu PILOT -> ADOPT

Progi są moimi propozycjami do kalibracji, nie danymi ze źródeł.
- **Judge:** zgodność z etykietami ludzkimi na własnym zbiorze >= ok. 80% (punkt odniesienia Zheng) i stabilność po zamianie kolejności; inaczej zostaje tylko sygnałem.
- **Routing/SLM:** spadek kosztu na rozmowę bez spadku jakości na zbiorze ewaluacyjnym ponad ustalony margines, mierzony na własnym ruchu.
- **Reflection:** poprawa na zbiorze z zewnętrznym walidatorem, bez wzrostu opóźnienia ponad budżet.
- **Pamięć sesji:** brak incydentów prywatności i zatwierdzenie podstawy prawnej.
- Każdy awans: jedna zmiana naraz (dyscyplina Brada), mierzone przed/po na stałym zbiorze, z rollbackiem.
