# Self research: koncepty LLM/agentów + czatbot na stronie + nauka modeli

- **Data researchu:** 2026-10-14 (źródła pobrane/wyszukane tego dnia; daty publikacji przy każdym źródle)
- **Tryb:** samodzielny research asystenta (WebSearch + WebFetch), **nie** przez workflow `research-gather` — brak niezależnej weryfikacji krzyżowej subagentów.
- **Powiązane:** [`../2026-10-14-agent-patterns-bradmurry/`](../2026-10-14-agent-patterns-bradmurry/) (główny research z blogów Brada Murry'ego + AutoGen)
- **Zasada oceny pewności:** **Wysoka** = pierwotne źródło (paper/oficjalna dokumentacja) pobrane i zacytowane; **Średnia** = jedno źródło lub paper znany tylko z abstraktu; **Niska** = blogi/streszczenia wtórne, liczby niezweryfikowane.
- **Czego NIE zrobiłem:** nie czytałem całych paperów (tylko abstrakty/oficjalne strony), nie uruchamiałem żadnego kodu, Medium pominięte na Twoją prośbę.

> **Założenia interpretacyjne (popraw mnie, jeśli źle):**
> 1. „Taxonomy" zinterpretowałem jako *taksonomię/klasyfikację wzorców agentowych* (nazwanie i pogrupowanie wzorców). Masz ją już de facto — patrz sekcja 1.
> 2. „Nauka modeli poniżej pojęcia" zinterpretowałem jako *uczenie/trenowanie modeli od poziomu początkującego* (małe modele, fine-tuning, trening od zera). Patrz sekcja 10.

---

## 1. Taxonomy (klasyfikacja wzorców agentowych)

**Co to jest:** nazwany podział wzorców projektowych na grupy wg problemu, który rozwiązują. Po co: wspólny język przy projektowaniu i wybór wzorca z drzewa decyzyjnego zamiast „na oko".

Istniejące taksonomie (każda patrzy pod innym kątem):

| Źródło | Oś podziału | Wzorce | Pewność |
|---|---|---|---|
| Anthropic, *Building Effective Agents* | **workflow vs agent**; 5 wzorców workflow | prompt chaining, routing, parallelization (sectioning/voting), orchestrator-workers, evaluator-optimizer | Wysoka |
| Liu i in., *Agent Design Pattern Catalogue* (arXiv 2405.10467, v1 16.05.2024, v4 06.11.2024) | architektura FM-agentów | 18 wzorców + „decision model for selecting the patterns" | Średnia (tylko abstrakt; grupowania nie widziałem) |
| Brad Murry, 13 wzorców (nasze źródła `05`, `09`–`21`) | główny *problem orkiestracji* | Flow, Authority, Delegation, Reasoning, Planning, Coordination (6 kategorii × 13 wzorców) | Wysoka (pełny tekst) |
| Huang i Zhou, *Two-Dimensional Framework* (arXiv 2605.13850) | dwie osie: topologia wykonania × funkcja poznawcza | m.in. katalogi Anthropic/Google ADK/LangChain | Niska (tylko snippet z wyszukiwarki, preprint) |

Cytaty (pobrane 2026-10-14):
- Anthropic: Workflows = „Systems where LLMs and tools are orchestrated through predefined code paths"; agenci: model sam wybiera kroki i narzędzia. Rada: zaczynaj od najprostszego rozwiązania, „adding complexity only when it demonstrably improves outcomes". https://www.anthropic.com/engineering/building-effective-agents
- Liu i in.: „a pattern catalogue consisting of 18 architectural patterns" + „decision model". https://arxiv.org/abs/2405.10467

**Wniosek (moja interpretacja):** taksonomię już masz — tabela „Flow/Authority/Delegation/Reasoning/Planning/Coordination" Brada to klasyfikacja *po problemie*, a podział Anthropic to klasyfikacja *po stopniu autonomii* (workflow → agent). Warto trzymać obie: najpierw „czy to workflow czy agent?", potem „który wzorzec z kategorii?".

Źródła: [Anthropic](https://www.anthropic.com/engineering/building-effective-agents), [Liu 2024](https://arxiv.org/abs/2405.10467), [2605.13850 (snippet)](https://www.alphaxiv.org/abs/2605.13850.md)

---

## 2. LLM-as-a-Judge

**Co to jest:** użycie modelu jako oceniającego (scoring lub porównanie A/B) zamiast człowieka/metryk. Skaluje ewaluację tam, gdzie nie ma jednej poprawnej odpowiedzi.

**Fakty (Wysoka):** Zheng i in., *Judging LLM-as-a-Judge with MT-Bench and Chatbot Arena* (arXiv 2306.05685, 09.06.2023; v4 24.12.2023):
- mocni sędziowie jak GPT-4 osiągają „over 80% agreement, the same level of agreement between humans";
- nazwane obciążenia: **position bias, verbosity bias, self-enhancement bias**; ograniczona zdolność rozumowania sędziego.
https://arxiv.org/abs/2306.05685

**Fakty (Średnia/Niska):** survey Gu i in. (arXiv 2411.15594) dzieli obciążenia na *task-agnostic* (od samego LLM, trudne do usunięcia) i *judgment-specific* (do ograniczenia projektem zadania); drugi survey (arXiv 2412.05579) dodaje m.in. authority i bandwagon bias. Znane mi tylko ze streszczeń wyszukiwarki.

**Praktyki ograniczania błędu (spójne w źródłach; część to moja synteza):**
- zamiana kolejności odpowiedzi i ponowna ocena (position bias) — Średnia;
- rozbicie rubryki na małe, binarne kryteria — Niska (blog);
- kalibracja względem zbioru etykiet ludzkich zanim zaufasz sędziemu — Średnia;
- sędzia z *innej rodziny modeli* niż generator (self-enhancement) — **to mój wniosek z nazwanego biasu, nie cytat**.

**Powiązanie z Bradem:** to dokładnie jego „Quality signal — opinion, not fact" (`07`): krytyk LLM = „the weakest signal: it drifts toward 'looks thorough'". Jego rozwiązanie: sędzia jako *wskazówka*, a decyzję podejmuje deterministyczny gate (recompute + diff). To spójne z literaturą.

Źródła: [Zheng 2023](https://arxiv.org/abs/2306.05685), [Gu survey](https://arxiv.org/pdf/2411.15594), [survey 2412.05579](https://arxiv.org/pdf/2412.05579)

---

## 3. Reflection

**Co to jest:** model ocenia/krytykuje własny wynik i poprawia go (pętla generate → critique → refine). Warianty: Self-Refine, Reflexion (refleksje trafiają do pamięci epizodycznej).

**Fakty:**
- **Reflexion** (Shinn i in., arXiv 2303.11366, 20.03.2023; v4 10.10.2023): wzmacnianie agentów „not by updating weights" lecz słowną refleksją zapisywaną w pamięci epizodycznej; deklarowane „91% pass@1 on HumanEval … surpassing … GPT-4 that achieves 80%". Wysoka (abstrakt, deklaracja autorów).
- **Kontrargument:** Huang i in., *Large Language Models Cannot Self-Correct Reasoning Yet* (arXiv 2310.01798, 03.10.2023; v2 14.03.2024): „LLMs struggle to self-correct their responses without external feedback", w niektórych przypadkach jakość *spada* po samokorekcie. Wysoka.
- Wtórne streszczenia (Niska): wąskim gardłem jest *wykrywanie* błędu, nie jego naprawa; zyski w paperach często zależą od zewnętrznych etykiet/oracle.

**Wniosek (moja synteza):** refleksja działa, gdy jest **zewnętrzny sygnał** (testy, wynik narzędzia, deterministyczny verify). Bez niego krytyka „z głowy" bywa bezwartościowa lub szkodliwa. To dokładnie wzorzec Generator/Critic/Refiner z `17` + gate z `22`–`25` u Brada (krytyk + budżet `size(drafts) < 4` + stan `stalled`).

Źródła: [Reflexion](https://arxiv.org/abs/2303.11366), [Huang 2023/24](https://arxiv.org/abs/2310.01798), [Self-Refine](https://arxiv.org/pdf/2303.17651), [Survey 2308.03188](https://arxiv.org/pdf/2308.03188)

---

## 4. Prompt Optimization

**Co to jest:** automatyczne strojenie promptów (instrukcje + przykłady few-shot) pod metrykę na zbiorze rozwojowym, zamiast ręcznego „prompt engineeringu".

**Fakty:**
- **GEPA** (Agrawal i in., arXiv 2507.19457, 25.07.2025; v2 14.02.2026; ICLR 2026 Oral): optymalizator oparty na refleksji w języku naturalnym i froncie Pareto; deklaracja autorów: „outperforms GRPO by 6% on average and by up to 20%, while using up to 35x fewer rollouts"; ponad 10% nad MIPROv2 (np. +12% na AIME-2025). Wysoka dla *deklaracji*, niezależnie niezweryfikowane.
- **DSPy:** instrukcje i demonstracje to parametry programu, optymalizatory (MIPROv2, SIMBA, GEPA) szukają wartości maksymalizujących metrykę. Niska/Średnia (blogi/tutoriale).
- **Zastrzeżenia (Niska, wtórne):** optymalizacja kosztuje wiele wywołań LLM; prompt może przeuczyć się na źle zdefiniowaną metrykę; prompt nie przenosi się niezawodnie między modelami.

**Powiązanie:** to ta sama pętla co Brad „self-evolving": RUN → MEASURE → DISCOVER → BUILD, „one change at a time", „report the ceiling" (`07`). GEPA robi to automatycznie na promptach; Brad na całym workflow/narzędziach.

Źródła: [GEPA](https://arxiv.org/abs/2507.19457), [DSPy overview (blog)](https://founderscreative.substack.com/p/automating-prompt-engineering-with), [agentpatterns.ai](https://agentpatterns.ai/agent-design/dspy-programmatic-prompt-optimization/)

---

## 5. SLM — Small Language Models

**Co to jest:** małe modele (zwykle < ~10 mld parametrów wg streszczeń — definicja nieostra) działające na sprzęcie konsumenckim, tanie i szybkie, dobre w wąskich, powtarzalnych zadaniach.

**Fakty (Wysoka dla stanowiska autorów):** Belcak i in. (NVIDIA Research), *Small Language Models are the Future of Agentic AI* (arXiv 2506.02153, 02.06.2025; wersja v3 widoczna z datą 22.09.2026): SLM są „sufficiently powerful, inherently more suitable, and necessarily more economical" dla wielu wywołań agentowych; systemy heterogeniczne (małe + duże) naturalnym wyborem tam, gdzie potrzebna ogólna rozmowa; paper podaje algorytm konwersji agentów LLM→SLM. To **position paper** (autorzy sami nazywają to „value statement" i zapraszają do krytyki) — nie dowód empiryczny.

**Gdzie to pasuje w praktyce:** wzorzec *routing* u Anthropic — „send simple queries to a smaller, cheaper model and hard ones to a more capable model". Czyli: SLM do klasyfikacji/ekstrakcji/routingu, duży model do trudnych kroków.

**Niska:** liczby typu „40–70% zapytań do zastąpienia", „10–30× taniej" pochodzą z blogów, nie z abstraktu — nie używać bez sprawdzenia w PDF.

Źródła: [arXiv 2506.02153](https://arxiv.org/abs/2506.02153), [Anthropic – routing](https://www.anthropic.com/engineering/building-effective-agents)

---

## 6. Prompt caching

**Co to jest:** dostawca zapamiętuje przetworzony *prefiks* promptu; kolejne zapytania z identycznym prefiksem są tańsze i szybsze.

**Anthropic (oficjalna dokumentacja, pobrana 2026-10-14; Wysoka):**
- prefiks = `tools` → `system` → `messages` do bloku z `cache_control`; trafienie wymaga **100% identycznego** prefiksu; do 4 jawnych breakpointów lub automatyczny top-level `cache_control`.
- TTL: 5 min (domyślnie, odświeżany przy użyciu) albo 1 h (`"ttl": "1h"`).
- ceny: zapis 5 min = **1.25×** bazowej ceny wejścia, zapis 1 h = **2×**, odczyt = **0.1×** (dla niektórych modeli 0.05× / 0.025×).
- minima tokenów zależą od modelu (np. 512 / 1 024 / 2 048 / 4 096) — poniżej minimum brak cache'u i brak błędu.
- zmiana definicji narzędzi unieważnia cały cache (tools → system → messages); timestamp w cache'owanym bloku = brak trafień.
- weryfikacja: pola `usage.cache_creation_input_tokens` i `usage.cache_read_input_tokens`.
https://platform.claude.com/docs/en/build-with-claude/prompt-caching

**OpenAI (oficjalna dokumentacja, pobrana 2026-10-14; Wysoka):**
- włączone domyślnie; minimum 1 024 tokenów (GPT-5.6+); retencja `prompt_cache_options.ttl` = „30m" (starsze modele: in-memory ~5–10 min lub 24 h); odczyt **0.1×**, zapis **1.25×**, „discounted up to 95%"; `usage.input_tokens_details.cached_tokens`.
https://developers.openai.com/api/docs/guides/prompt-caching

**Praktyka (z dokumentacji):** stałe rzeczy na początek (instrukcje, narzędzia, dokumenty, przykłady), zmienne na koniec; nic zmiennego w cache'owanym prefiksie; monitoruj hit rate. **Uwaga:** ceny/TTL zmieniają się — traktuj jako stan na 2026-10-14.

**Dla czatbota:** system prompt + opis sklepu/FAQ/katalog = idealny kandydat na cache'owany prefiks.

---

## 7. Long-term memory (pamięć długoterminowa)

**Co to jest:** informacje przeżywające pojedynczą sesję/okno kontekstu: fakty o użytkowniku, historia, wyuczone reguły.

**Fakty:**
- **MemGPT** (Packer i in., arXiv 2310.08560, 12.10.2023): „virtual context management" wzorowane na hierarchii pamięci systemów operacyjnych — przenoszenie danych między „szybką i wolną" pamięcią, przerwania do sterowania przepływem. Wysoka (abstrakt).
- **Mem0** (Chhikara i in., arXiv 2504.19413, 28.04.2025): wyciąga, konsoliduje i wyszukuje fakty z rozmów; deklaracje autorów na LOCOMO: +26% względnie (LLM-as-a-Judge) vs OpenAI, −91% opóźnienia p95 i >90% mniej tokenów vs pełny kontekst. **Wysoka dla deklaracji, ale to paper autorów produktu** — nie niezależny benchmark.
- **Reflexion** trzyma refleksje w pamięci epizodycznej (sekcja 3).
- **Typologia** semantic (fakty) / episodic (doświadczenia) / procedural (reguły „jak") — Niska (blogi: dev.to, forkast.news), ale spójna między źródłami i narzędziami (Letta, Mem0, LangMem wg streszczeń).

**Powiązanie z Bradem:** jego „context store" to pamięć *w obrębie jednego przebiegu* (typed context, journal, replay). Pamięć długoterminowa między przebiegami to osobna warstwa; jego zasada: „if a value must be durable and inspectable, it belongs in context" (`03`).

**Ryzyka (moja ocena):** pamięć użytkownika = dane osobowe (RODO), zatrucie pamięci (OWASP LLM04), potrzeba kasowania/„zapomnienia".

Źródła: [MemGPT](https://arxiv.org/abs/2310.08560), [Mem0](https://arxiv.org/abs/2504.19413), [dev.to guide](https://dev.to/dublecc/how-ai-agent-memory-works-in-2026-context-episodic-semantic-and-procedural-l3f)

---

## 8. Agent loop (pętla agenta)

**Co to jest:** podstawowa pętla: *wywołaj model → sparsuj wyjście → wykonaj narzędzia → dołącz wyniki do kontekstu → powtórz*, aż do warunku stopu.

**Fakty:**
- **ReAct** (Yao i in., arXiv 2210.03629, 06.10.2022; v3 10.03.2023): przeplatanie śladów rozumowania i akcji; „ReAct overcomes issues of hallucination and error propagation prevalent in chain-of-thought reasoning". Wysoka.
- **Anthropic:** agenci działają w pętli użycia narzędzi sterowanej informacją zwrotną ze środowiska; „Stopping conditions, such as a maximum iteration count" utrzymują kontrolę; autonomia = wyższy koszt i ryzyko kumulacji błędów. Wysoka.
- **Warunki stopu (Niska, blogi, spójne):** cel osiągnięty, limit iteracji, budżet tokenów/kosztu, kolejne błędy narzędzia, anulowanie przez użytkownika; log append-only każdego kroku do replay.

**Dwa style sterowania (porównanie z naszym researchem):**
| | Pętla modelu (ReAct) | Pętla maszyny (Brad: reactive reducer) |
|---|---|---|
| Kto decyduje o następnym kroku | model | zadeklarowane krawędzie + guardy |
| Stop | model „uważa, że skończył" + limity | stan terminalny, `maxIterations`, cap 128 mikrokroków, no-progress guard |
| Atut | elastyczność przy otwartych zadaniach | determinizm, replay, audyt |

Brad ostrzega też: no-progress guard działa tylko *w obrębie jednego* `advance()`, nie między turami (`13`, `14`, `16`) — pętle międzyturowe pilnuje enum werdyktu + `maxIterations`.

Źródła: [ReAct](https://arxiv.org/abs/2210.03629), [Anthropic](https://www.anthropic.com/engineering/building-effective-agents)

---

## 9. Własny czatbot na stronie (jak to ułożyć)

**Architektura minimalna (moja synteza źródeł poniżej):**

```
Przeglądarka (widget: HTML/JS, SSE)
        │  HTTPS, bez żadnych kluczy
        ▼
Twój backend (proxy)  ──►  limity: per-IP/użytkownik + budżet kosztowy
   │   │   │                walidacja wejścia, logowanie
   │   │   └──► pamięć/sesja (baza)
   │   └──────► RAG: wyszukiwanie w treściach strony/FAQ/katalogu
   ▼
Model: (A) API dostawcy  lub  (B) własny SLM (Ollama/llama.cpp/vLLM)  lub  (C) router A+B
```

Kluczowe zasady (Średnia/Niska — blogi bezpieczeństwa; baza autorytatywna: OWASP):
1. **Klucz API tylko po stronie serwera**, nigdy w JS ani w repo; wszystkie wywołania LLM przez backend-proxy.
2. **Rate limit liczy żądania, nie koszt** — dodaj budżet per użytkownik/dzień i `max_tokens`.
3. **Prompt injection nie zatrzyma sam rate limit** — oddziel instrukcje systemowe od treści użytkownika, treść pobraną ze stron traktuj jako niezaufaną (injection pośrednia), ogranicz uprawnienia narzędzi.
4. **Zakładaj, że system prompt wycieknie** — nie wkładaj tam sekretów.
5. **OWASP Top 10 for LLM Applications 2025** (pobrane 2026-10-14, Wysoka): LLM01 Prompt Injection, LLM02 Sensitive Information Disclosure, LLM03 Supply Chain, LLM04 Data and Model Poisoning, LLM05 Improper Output Handling, LLM06 Excessive Agency, LLM07 System Prompt Leakage, LLM08 Vector and Embedding Weaknesses, LLM09 Misinformation, LLM10 Unbounded Consumption. https://genai.owasp.org/llm-top-10/
6. **Streaming:** odpowiedź przez SSE (`stream: true`) daje płynne wyświetlanie tokenów; trzeba obsłużyć błąd w środku strumienia. https://platform.claude.com/docs/en/build-with-claude/streaming
7. **Cache'uj prefiks** (sekcja 6) i **mierz** hit rate.
8. **Ewaluacja:** zbiór pytań testowych + sędzia LLM skalibrowany na ręcznych etykietach (sekcja 2) + deterministyczne sprawdzenia tam, gdzie się da (np. „czy odpowiedź zawiera istniejący SKU/URL").

**API vs własny model (Niska — blogi porównawcze):**
- *API dostawcy:* najszybszy start, najlepsza jakość, koszt per token, dane wychodzą poza Twoją infrastrukturę.
- *Własny SLM:* kontrola i prywatność, niski koszt przy dużym ruchu, ale utrzymanie i słabsza jakość ogólna. Wg streszczeń: **Ollama** — najłatwiejszy dla jednego użytkownika, **llama.cpp** — najwięcej kontroli/kwantyzacji, **vLLM** — wygrywa przy wielu równoległych użytkownikach (potrzebuje GPU). Liczby tokenów/s z blogów są sprzeczne — benchmark na własnym sprzęcie.
- *Hybryda (router):* mały model do klasyfikacji/FAQ, duży do trudnych pytań (patrz SLM + routing).

**Gotowce (Niska):** Open WebUI (self-hosted UI do Ollama/API z RAG), pluginy WordPress typu „AI Chatbot". Nie sprawdzałem ich jakości ani licencji.

**Związek z Twoim ECommerceApp (tylko sugestia):** czatbot sklepu = RAG po katalogu/FAQ/polityce zwrotów, bez uprawnień do zmiany zamówień (LLM06 Excessive Agency). Akcje (np. status zamówienia) tylko przez istniejące, autoryzowane endpointy.

---

## 10. Uczenie modeli „od poziomu zero" (drabinka)

Od najtańszego i najbezpieczniejszego do najbardziej „własnego":

1. **Prompt + kontekst** (zero treningu) — zacznij tu; Anthropic: „simplest solution first".
2. **RAG** — wiedza z Twoich dokumentów w prompcie. Według tutoriali fine-tuning słabo uczy *nowych faktów*; służy raczej stylowi/formatowi/zachowaniu (Niska, blogi).
3. **Fine-tuning SLM metodą LoRA/QLoRA.** LoRA zamraża wagi bazowe i trenuje małe macierze adaptera niskiego rzędu `r`; po treningu adapter można scalić (`merge_and_unload()`). Wysoka: oficjalna dokumentacja Hugging Face PEFT. Wg tutoriali (Niska) 7B da się dostroić na jednej karcie ~24 GB z kwantyzacją. https://huggingface.co/docs/peft/main/en/conceptual_guides/lora
4. **Trening malutkiego modelu od zera — żeby zrozumieć, jak to działa:**
   - **nanoGPT** (Karpathy): README oznacza repo jako przestarzałe (aktualizacja z listopada 2025, odsyła do nanochat), ale quick start nadal pokazuje trening znakowego GPT na Szekspirze: ok. 3 min na jednym A100 lub ok. 3 min na laptopie z mniejszym modelem (loss 1.88 — słabsze próbki). https://github.com/karpathy/nanoGPT
   - **nanochat** (Karpathy): „The best ChatGPT that $100 can buy"; README: model o możliwościach GPT-2 za „$48 (~2 hours of 8XH100 GPU node)", na spocie ok. $15; pełny pipeline (tokenizacja → pretraining → finetuning → eval → inference); „talking to a kindergartener". CPU/MPS tylko w wersji zmniejszonej, „You will not get strong results". https://github.com/karpathy/nanochat

**Pytanie do Ciebie:** jeśli „poniżej pojęcia" znaczyło coś innego (np. *od zera dla laika*, *bez GPU*, *model bez internetu*), powiedz — dopasuję.

---

## 11. Jak to się skleja w jedną całość (synteza, moja)

- **Taksonomia** (1) pomaga *wybrać* wzorzec; **pętla agenta** (8) to jego silnik; **reflection** (3) i **LLM-as-judge** (2) to mechanizmy jakości — oba potrzebują *zewnętrznego sygnału* albo skalibrowanego sędziego.
- **Prompt optimization** (4) i **SLM** (5) to dźwignie kosztu/jakości; **prompt caching** (6) to dźwignia kosztu bez zmiany jakości; **pamięć** (7) to dźwignia ciągłości między sesjami.
- Wspólny wątek wszystkich źródeł Brada i literatury: *model wnosi wartość, a kontrola, limity i weryfikacja siedzą w deterministycznej powłoce.*

## 12. Luki i zastrzeżenia

- Nie otwierałem pełnych paperów — tylko abstrakty i oficjalne strony (liczby „deklarowane przez autorów" ≠ niezależne benchmarki).
- Taksonomia 2605.13850, survey LLM-as-judge (2411.15594, 2412.05579) i Self-Refine tylko ze snippetów wyszukiwarki.
- Ceny, minima tokenów i TTL cache'u to stan z dokumentacji na 2026-10-14; zmieniają się często.
- Porównania Ollama/llama.cpp/vLLM i rady bezpieczeństwa czatbota pochodzą z blogów (część komercyjnych) — do potwierdzenia we własnym teście i w OWASP.
- Brak własnych eksperymentów: żadna liczba z tego dokumentu nie została odtworzona.

## 13. Pełna lista źródeł (z datami)

| Źródło | Typ | Data | Użyto w |
|---|---|---|---|
| [Anthropic – Building Effective Agents](https://www.anthropic.com/engineering/building-effective-agents) | oficjalny blog | pobrane 2026-10-14 | 1, 5, 8, 11 |
| [Liu i in., Agent Design Pattern Catalogue](https://arxiv.org/abs/2405.10467) | paper | 2024-05-16 (v4 2024-11-06) | 1 |
| [Zheng i in., MT-Bench / LLM-as-a-Judge](https://arxiv.org/abs/2306.05685) | paper | 2023-06-09 (v4 2023-12-24) | 2 |
| [Gu i in., Survey on LLM-as-a-Judge](https://arxiv.org/pdf/2411.15594) | survey (snippet) | — | 2 |
| [Reflexion](https://arxiv.org/abs/2303.11366) | paper | 2023-03-20 (v4 2023-10-10) | 3, 7 |
| [Huang i in., LLMs Cannot Self-Correct Reasoning Yet](https://arxiv.org/abs/2310.01798) | paper | 2023-10-03 (v2 2024-03-14) | 3 |
| [GEPA](https://arxiv.org/abs/2507.19457) | paper (ICLR 2026 Oral) | 2025-07-25 (v2 2026-02-14) | 4 |
| [SLM are the Future of Agentic AI](https://arxiv.org/abs/2506.02153) | position paper | 2025-06-02 (v3 2026-09-22) | 5 |
| [Anthropic – Prompt caching docs](https://platform.claude.com/docs/en/build-with-claude/prompt-caching) | dokumentacja | pobrane 2026-10-14 | 6 |
| [OpenAI – Prompt caching docs](https://developers.openai.com/api/docs/guides/prompt-caching) | dokumentacja | pobrane 2026-10-14 | 6 |
| [MemGPT](https://arxiv.org/abs/2310.08560) | paper | 2023-10-12 (v2 2024-02-12) | 7 |
| [Mem0](https://arxiv.org/abs/2504.19413) | paper | 2025-04-28 | 7 |
| [ReAct](https://arxiv.org/abs/2210.03629) | paper | 2022-10-06 (v3 2023-03-10) | 8 |
| [OWASP Top 10 for LLM Apps 2025](https://genai.owasp.org/llm-top-10/) | standard | pobrane 2026-10-14 | 9 |
| [Anthropic – Streaming](https://platform.claude.com/docs/en/build-with-claude/streaming) | dokumentacja | pobrane 2026-10-14 | 9 |
| [HF PEFT – LoRA](https://huggingface.co/docs/peft/main/en/conceptual_guides/lora) | dokumentacja | pobrane 2026-10-14 | 10 |
| [karpathy/nanoGPT](https://github.com/karpathy/nanoGPT) | README | pobrane 2026-10-14 | 10 |
| [karpathy/nanochat](https://github.com/karpathy/nanochat) | README | pobrane 2026-10-14 | 10 |
| Blogi/porównania (dev.to, forkast.news, morphllm, insiderllm, reintech, enterno, webreference, tutoriale LoRA) | wtórne | różne | oznaczone „Niska" |
