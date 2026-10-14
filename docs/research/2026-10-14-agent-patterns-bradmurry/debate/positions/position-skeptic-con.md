# Debater: con, Round 1, position

Perspektywa: sceptyk. Skala dowodów: STRONG = źródło pierwotne/niezależne, MEDIUM = jedno jawne źródło, WEAK = samoopis jednego autora.

## Tezy

1. **Runtime „Aegis" / reactive reducer to jednoosobowy stos bez weryfikacji.** Wszystkie twierdzenia o runtime pochodzą od jednego autora, a w materiale nie ma publicznie zweryfikowanej implementacji [00-summary#Gaps]. Korelacja „determinizm, replay, ograniczony koszt" ma tylko Brada, „kilka postów to nie niezależne potwierdzenie" [01#2]. Teza „the glue code is gone" ma werdykt Unsupported (D1) [01#4]. Dowód: WEAK. Importowanie FSM-owego DSL (CEL, mikrokroki, cap 128) do .NET to koszt bez gwarancji. W repo mamy już Outbox/Inbox i saga engine, więc „deterministyczna powłoka" jest naszym własnym kodem.

2. **Metryki „baseline vs optimum" nie są dowodem.** To samoopisy bez prób i powtórzeń, a nie wiadomo, czy to realne uruchomienia [00-summary#Gaps]. Autor w jednym przykładzie nazywa „optimum" wartość wyższą niż po Round 2 (41 000 vs 38 000 tokenów, C5) [01#1]. Finalny FSM w przykładzie entity resolution nie ma bloku `policy`, choć seria uczy, że to zabezpieczenie (C6). Dowód: WEAK.

3. **Autor sam osłabia swoje gwarancje.** Intro serii obiecuje, że pętlę ogranicza m.in. no-progress guard. Późniejsze posty to korygują: guard „does not span turns", zostają dwa zabezpieczenia zamiast trzech (C1) [01#1]. Intro nie poprawiono. Dla czatbota, czyli rozmowy złożonej z wielu tur, to właśnie luka, która dotyczy nas najbardziej. Dowód: MEDIUM (jawna autokorekta).

4. **Debata i quorum: brak dowodu skuteczności, a koszt jest pewny.** Zebrany materiał nie zawiera danych o skuteczności debaty (korelacja: „Low") [01#2]. Jedyny przykład AutoGen kończy się pełną zgodnością [00-summary#C]. Brad sam wskazuje, że wspólne ślepe punkty wymagają różnorodności modeli, nie innej pętli, a quorum ma korelację błędów „nie do naprawy mechanizmem" [00-summary#B, wzorce 6 i 12]. Koszt to debaterzy × rundy × (pozycje + krytyki) + sędzia. Kilka kopii tego samego modelu głosujących większością odtwarza ten sam błąd. Dowód przeciw: MEDIUM (przyznanie autora). Zewnętrzny paper o debacie (arXiv 2406.11776) nie został pobrany [01#2].

5. **Reflection i LLM-as-judge bez zewnętrznego sygnału są ryzykowne.** To najlepiej poparty punkt, ale wspiera sceptycyzm: Huang i in. — „struggle to self-correct… without external feedback", a jakość czasem spada [self-research#3]. Zheng i in. nazywają position, verbosity i self-enhancement bias [self-research#2]. Brad nazywa krytyka LLM „the weakest signal" [01#2]. Wniosek: sędzia jest użyteczny tylko skalibrowany na etykietach ludzkich. Pętla Generator/Critic/Refiner na odpowiedziach czatbota bez weryfikacji (np. czy SKU istnieje) dodaje koszt i opóźnienie bez korzyści. Dowód: STRONG (3 niezależne grupy).

6. **AutoGen: proza i kod się rozjeżdżają, a strona sama ostrzega.** Reflection: proza wspomina „max iterations", kod nie ma limitu, więc możliwa pętla nieskończona (C9). Group chat: strona pisze, że nie jest „meant to be used in real applications" (C10). Debate: sąsiedztwo i typ wiadomości w prozie ≠ kod (C7, C8). Mixture-of-agents: 3 warstwy w prozie, 2 w kodzie (C11) [01#1]. To materiał edukacyjny, nie wzorzec produkcyjny. Dowód: MEDIUM, ale bezpośrednio sprawdzalny w kodzie.

7. **Pamięć długoterminowa to obciążenie prawne i bezpieczeństwa, zanim stanie się funkcją.** Deklaracje Mem0 (+26%, −91% opóźnienia) pochodzą z papera autorów produktu, a nie z niezależnego benchmarku [self-research#7]. Ryzyka (moja ocena, nie źródło): dane osobowe i RODO, zatrucie pamięci (OWASP LLM04), prawo do usunięcia. Czatbot sklepu nie potrzebuje wiedzieć „kim jesteś" między sesjami, żeby odpowiedzieć na pytanie o zwroty. Dowód korzyści: WEAK. Dowód ryzyka: STRONG (OWASP).

8. **SLM i prompt optimization: obiecujące, ale nie na start.** SLM to position paper, który sami autorzy nazywają „value statement"; liczby typu „10–30× taniej" są z blogów (Low) [01#4 D6; self-research#5]. GEPA to deklaracje autorów bez niezależnej weryfikacji, a optymalizacja kosztuje wiele wywołań i może przeuczyć się na złą metrykę [self-research#4]. Bez zbioru ewaluacyjnego nie ma czego optymalizować. Dowód: MEDIUM/WEAK.

Dodatkowo: Mastra i Medium dały 0 faktów, więc ich wzorce nie wchodzą do katalogu [00-summary#Gaps]. Self-research nie był weryfikowany krzyżowo i nie odtworzono żadnej liczby [self-research#12].

## Co przyznaję jako dobrze poparte
Prompt caching (oficjalna dokumentacja, odczyt 0.1×) [self-research#6], limity pętli poza modelem (Anthropic + Brad), zasada „najprostsze najpierw" (Anthropic; Brad: „a for-loop could do for free") [01#2], OWASP Top 10 LLM i ograniczenie uprawnień (LLM06).

## Czego bym NIE budował
- Runtime reducer/FSM Brada, debaty, quorum, auction, dynamic team, swarm.
- Pętli reflection bez zewnętrznego sygnału oraz auto-ewoluującego agenta.
- Pamięci długoterminowej o użytkowniku; własnego/dostrajanego SLM; automatycznej optymalizacji promptów.

## Co zbudowałbym zamiast
Jedno wywołanie modelu z RAG po FAQ/katalogu/polityce zwrotów, prompt chaining lub routing jako zwykły kod C#. Klucz API tylko po stronie serwera, limity kosztowe i `max_tokens`, twardy limit iteracji. Akcje (np. status zamówienia) tylko przez istniejące autoryzowane endpointy, z danymi sesji bez trwałej pamięci. Cache prefiksu systemowego. Mały zbiór testów z deterministycznymi sprawdzeniami (istnieje SKU/URL). Kolejność: caching i guardrails, potem eval, dopiero wtedy sędzia, router lub SLM, jeśli pomiar to uzasadni.
