# Decyzja: wzorce agentowe dla ECommerceApp (czatbot na stronie)

Źródła decyzji: debate/positions/{pro,con,prag}.md, debate/critiques/round1-*.md, judge-round1.md.
Skala dowodów: STRONG oznacza źródło pierwotne lub niezależne (kilka grup). MEDIUM oznacza jedno jawne źródło. WEAK oznacza samoopis jednego autora lub blogi.
Wszystkie liczby progów poniżej są oznaczone jako **propozycja**. Żadna nie pochodzi ze źródeł.

## (a) Tiery

### ADOPT NOW
| Element | Powód (1 linia) | Siła dowodu |
|---|---|---|
| Proxy backendowe ASP.NET, klucz API tylko na serwerze | Zapobiega wyciekowi klucza i niekontrolowanemu użyciu (OWASP LLM10) | STRONG (OWASP, README §9 pkt 5) |
| Limity: `max_tokens`, limit tur, budżet kosztowy z kill-switchem, rate limit per sesja/IP | Unbounded Consumption (LLM10). Limit tur jest konieczny, bo guard Brada „does not span turns” (01 C1) | STRONG dla listy ryzyk; konkretne wartości to propozycja |
| Brak narzędzi zapisujących; odczyt (status zamówienia) wyłącznie przez istniejące autoryzowane endpointy | Excessive Agency (LLM06) | STRONG (OWASP) |
| Najprostsze najpierw: workflow w kodzie C# zamiast autonomicznego agenta | Anthropic i Brad niezależnie („a for-loop could do for free”) | STRONG, 01 §2 High |
| Jawny limit iteracji i stop poza modelem | Anthropic („Stopping conditions, such as a maximum iteration count”) i Brad | STRONG kierunkowo (01 §2: niezależność „częściowo”; ReAct **nie** jest źródłem limitów) |
| Prompt caching stałego prefiksu (system prompt, FAQ, polityki), monitoring cache-read | Dokumentacja dostawców: odczyt 0.1×, zapis 1.25× / 2× | STRONG (oficjalna dokumentacja, stan 2026-10-14) |
| RAG po katalogu, FAQ i politykach zwrotów; treść z RAG traktowana jako niezaufana | Wiedza sklepu bez fine-tuningu; ryzyko pośredniej prompt injection (LLM01) | MEDIUM/WEAK jako praktyka (README §9 to „moja synteza”, blogi); OWASP STRONG |
| Deterministyczne post-walidatory: czy SKU, URL i cena istnieją i zgadzają się z bazą | Ogranicza halucynowane produkty i ceny (LLM09); zewnętrzny sygnał zamiast samokrytyki | STRONG dla zasady „zewnętrzny sygnał” (Huang; 01 §2). Sam mechanizm to praktyka (README §9 pkt 8, WEAK) |

### PILOT
| Element | Powód | Siła dowodu |
|---|---|---|
| Zbiór ewaluacyjny z realnych zgłoszeń + checki deterministyczne | Bez niego nie da się awansować niczego z PILOT ani DEFER | MEDIUM (README §2, §9 pkt 8) |
| LLM-as-judge jako sygnał pomocniczy, kalibrowany na etykietach ludzkich, z zamianą kolejności odpowiedzi | Zheng: „over 80% agreement” na MT-Bench, ale z biasami position, verbosity i self-enhancement | STRONG co do biasów; MEDIUM co do przenośności na sklep |
| Routing do tańszego modelu lub SLM dla FAQ i klasyfikacji intencji | Wzorzec routing Anthropic; paper SLM to position paper („value statement”) | MEDIUM/WEAK (01 D6) |
| Reflection wyłącznie z zewnętrznym walidatorem | Huang: bez zewnętrznego feedbacku jakość bywa gorsza | STRONG jako ostrzeżenie; skuteczność w sklepie nieznana |
| Krótka pamięć sesji (bez danych osobowych, bez trwałości między sesjami) | Ciągłość rozmowy | brak źródła (wniosek prag, G4 con) |

### DEFER
| Element | Powód | Siła dowodu |
|---|---|---|
| Pamięć długoterminowa użytkownika (Mem0, MemGPT) | Liczby Mem0 pochodzą z papera autorów produktu; RODO; ryzyko zatrucia | korzyść WEAK; ryzyko MEDIUM (README §7 „moja ocena”, **nie** STRONG) |
| Automatyczna optymalizacja promptów (GEPA, DSPy) | Deklaracje autorów; najpierw musi istnieć zbiór ewaluacyjny i metryka | MEDIUM/WEAK (README §4) |
| Własny hosting SLM, fine-tuning LoRA | Koszt utrzymania przy małym ruchu bez dowodu korzyści | WEAK |
| Akcje z efektem (zwrot, anulowanie) wykonywane przez czatbota | Patrz §c, punkt sporny 3 | idempotencja/retry: wzorzec klasyczny; przeniesienie na agenta tylko u Brada (WEAK/MEDIUM) |
| Pętla self-evolving (RUN/MEASURE/DISCOVER/BUILD) | Metryki Brada to samoopis bez prób (01 §2 Low–Medium, C5) | WEAK |

### SKIP
| Element | Powód | Siła dowodu |
|---|---|---|
| Runtime reactive reducer „Aegis” jako zależność | Jeden autor, brak publicznej, zweryfikowanej implementacji; „the glue code is gone” = Unsupported | 00 Gaps, 01 D1 |
| Debata multi-agent (6) | Brak danych o skuteczności w materiale (01 §2 Low); koszt rośnie z liczbą debaterów i rund | Low |
| Quorum (12), aukcja (10), dynamic team (13), swarm (5) | Czatbot sklepu ich nie potrzebuje. Dla quorum Brad sam wskazuje skorelowane błędy. Dla 10 i 13 w materiale **nie ma żadnej** oceny skuteczności, więc „Low” z 01 §2 ich nie obejmuje | brak dowodu (nie: dowód przeciw) |
| Kod przykładów AutoGen jako kod produkcyjny | Rozbieżności proza/kod C7–C12, np. reflection bez limitu iteracji; strona group chat: „not meant to be used in real applications” | MEDIUM, bezpośrednio sprawdzalne |
| Wzorce z Mastra i Medium | 0 faktów (treść niedostępna) | brak danych |

## (b) Minimalna architektura czatbota (uzgodniona)
1. Widget JS (SSE) łączy się z endpointem ASP.NET (proxy). Klucz i prompty są tylko na serwerze. Należy zakładać, że system prompt wycieknie (LLM07).
2. Klasyfikacja intencji z wyjściem w postaci skończonego zbioru wartości (np. FAQ / status zamówienia / zwroty / człowiek), a dalszy przepływ jest w kodzie C# (`switch`). **Moment wprowadzenia jest sporny, patrz §c.1.** Wersja minimalna to jedno wywołanie modelu z RAG.
3. RAG po katalogu, FAQ i politykach. Pobrana treść jest niezaufana.
4. Prompt składa się ze stałego prefiksu (cache) i zmiennych na końcu. Monitorowane są tokeny odczytu z cache.
5. Odczyt danych użytkownika (status zamówienia) wyłącznie przez istniejące, autoryzowane zapytania, po uwierzytelnieniu lub weryfikacji. Zero komend zapisu. Eskalacja do człowieka jest stanem końcowym.
6. Limity: `max_tokens`, limit tur, budżet dzienny z kill-switchem, rate limit. Wartości to **propozycja** do ustalenia.
7. Post-walidator deterministyczny (SKU, URL, cena). Przy niezgodności czatbot nie wysyła treści i odsyła do bezpiecznej odpowiedzi lub do człowieka.
8. Logowanie rozmów z minimalizacją danych osobowych i obsługą kasowania.
9. Bez pamięci długoterminowej. Pamięć sesji tylko jako PILOT.
10. Ewaluacja offline: zbiór pytań (start ok. 50–100 pytań z realnych zgłoszeń, **propozycja** prag), checki deterministyczne, judge pomocniczy z innej rodziny modeli niż generator (wniosek, nie cytat).

Uwaga z repo (prag P2): Outbox/Inbox i sagi istnieją, ale nie obsługują wywołań LLM, a w kodzie nie ma integracji LLM. Dla v1 tylko do odczytu nie trzeba budować nowej „deterministycznej powłoki” (journal, replay).

## (c) Zachowane zdanie odrębne
1. **Router intencji jako enum od razu (pro) czy po pomiarze (con).** Pro chce router w drugim kroku. Con zaczyna od jednego wywołania z RAG, a router dodaje dopiero, gdy pomiar to uzasadni. Prag stoi pośrodku: router w kodzie, bez odwołania do wzorca 3 Brada (ma udokumentowany błąd „twice… does nothing”). Żadne źródło nie mierzy zysku z routera w czatbocie sklepu.
2. **Nazwane wzorce Brada (3, 8, 9) czy zwykły C#.** Pro chce ich używać jako słownika. Con i prag chcą kodu bez etykiet, bo wzorzec 9 w wersji Brada re-emituje cały artefakt, a check SKU to zwykły post-walidator.
3. **Akcje z efektem (zwrot).** Pro dopuszcza je po wdrożeniu idempotencji i journala, z retry z **tym samym** kluczem (pro skorygował się zgodnie z bm-series-parts A6). Prag dopuszcza je tylko przez istniejącą `RefundSaga` lub endpoint, po pilocie tylko do odczytu. Con nie proponuje ich w ogóle; „con wyklucza” to charakterystyka dokonana przez prag, nie wprost con. Wspólne minimum: v1 jest tylko do odczytu.
4. **Czy kierunkowa zgodność zasługuje na STRONG.** Con i prag: nie, jeśli niezależne źródła (Zheng, Huang) dotyczą innego zagadnienia niż twierdzenie. Pro przy #1 pozostał przy STRONG kierunkowo. Sędzia uznaje przypisanie Zhenga do reducera za błędne (judge-round1).

## (d) Kryteria awansu i co zmieniłoby decyzję
Wszystkie progi to **propozycja** (prag §c) do kalibracji na własnych danych.
- **Judge PILOT→ADOPT:** zgodność z etykietami ludzkimi na własnym zbiorze w okolicy punktu odniesienia Zheng (ok. 80%, MT-Bench, nie sklep) i stabilność po zamianie kolejności. W przeciwnym razie judge zostaje sygnałem pomocniczym.
- **Routing/SLM:** niższy koszt na rozmowę bez spadku jakości na zbiorze ewaluacyjnym ponad ustalony margines. Margines to propozycja.
- **Reflection:** poprawa na zbiorze z zewnętrznym walidatorem bez przekroczenia budżetu opóźnienia.
- **Pamięć sesji:** zatwierdzona podstawa prawna (RODO) i brak incydentów prywatności w pilocie.
- **Akcje z efektem (DEFER→PILOT):** idempotentny klucz tury, retry z tym samym kluczem przy UNKNOWN, przejście przez istniejącą sagę lub endpoint, eskalacja do człowieka po wyczerpaniu prób.
- **Dyscyplina każdego awansu:** jedna zmiana naraz, pomiar przed i po na stałym zbiorze, rollback.
- **Co zmieniłoby decyzję:**
  - niezależny benchmark lub publiczne repo Aegis mogłoby przenieść reducer do PILOT;
  - dane o skuteczności debaty z innymi modelami (np. nieprzeczytany arXiv 2406.11776) mogłyby przenieść debatę do DEFER;
  - niezależny benchmark Mem0 lub GEPA mógłby przenieść te elementy do PILOT;
  - własny pomiar pokazujący, że routing obniża koszt bez straty jakości, rozstrzygnąłby §c.1.

## (e) Czego baza dowodów NIE mówi
- Nic o czatbocie e-commerce wprost. Żadne źródło nie mierzy RAG, routera, judge ani limitów w sklepie internetowym.
- Nic o .NET. Wszystkie przykłady to Python, YAML i CEL.
- Brak niezależnej weryfikacji runtime'u Aegis i metryk Brada (bez prób i powtórzeń; C5 jest wewnętrznie niespójne).
- Brak danych o skuteczności debaty i quorum; dla aukcji i dynamic team brak jakiejkolwiek oceny.
- Self-research nie był weryfikowany krzyżowo i nie odtworzono w nim żadnej liczby (README §12). Część źródeł znana jest tylko z abstraktów lub snippetów.
- 5,2% cytatów w findings nie zgodziło się dosłownie z tekstem źródeł. Nie przejrzano ich wszystkich (01 §5).
- Brak danych o kosztach i ruchu ECommerceApp. Opłacalność cachingu, SLM i limitów trzeba zmierzyć.
- **Konsensus nie jest niezależnym potwierdzeniem.** Trzej debaterzy pracowali na jednej bazie przygotowanej przez jednego asystenta, z materiałem zdominowanym przez jednego autora (Brad). Zgodność stanowisk może odtwarzać wspólny bias syntezy, np. grupowanie „Brad + Zheng + Huang” w 01 §2.

---

## Erratum (2026-10-14, po przeglądzie założeń względem kodu)
- **§a (ADOPT NOW, post-walidatory) i §b pkt 7:** zapis „SKU, URL i cena" jest **błędny dla tego repo**. `ECommerceApp.Domain/Catalog/Products/Product.cs` ma `Name` i `Cost` (cena); **nie ma SKU ani pola URL**; adres produktu to trasa `{id:int}` w `StorefrontController`. Walidator ma sprawdzać **Id produktu, trasę i cenę**. Źródło: [przegląd założeń](../../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md) §3.
- **§b pkt 5 (odczyt statusu zamówienia):** autoryzacja istnieje (`OrderAccessAuthorizationHandler`, `VerificationCode*`), ale to przepływy MVC/cookie. Czatbot musiałby działać jako zalogowany klient albo gość z zakresem jednego zamówienia (`GuestAccessScope`). Wykonalność z backendowego proxy nie została sprawdzona (hipoteza H7 w przeglądzie).
- Pozostałe pliki debaty (`positions/`, `critiques/`, `judge-round1.md`) zawierają to samo założenie „SKU/URL/cena" jako zapis historyczny i nie były edytowane.
- **Zmiana nazwy pliku:** `debate/positions/con.md` został przemianowany na `debate/positions/position-skeptic-con.md`. Nazwa `con` jest zarezerwowana w Windows i uniemożliwiała zapis w git. Odniesienia „positions/{pro,con,prag}.md" w tekście oznaczają stanowiska: pro, sceptyk (con), pragmatyk (prag).
