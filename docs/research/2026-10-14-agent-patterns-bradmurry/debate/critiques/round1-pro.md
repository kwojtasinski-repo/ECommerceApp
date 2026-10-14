# Debater: pro, Round 1, critique

Skróty: 00 = analysis/00-summary.md, 01 = analysis/01-cross-source-verification.md, LSR = llm-self-research/README.md. Otwarte i sprawdzone: 00, 01, LSR, bm-patterns-1-7-raw (F5c/F6g), bm-patterns-8-13-raw (F8.9), autogen-raw-9pages, bm-series-parts-raw.

## Krytyka con

1. **con#1** ("runtime Aegis to jednoosobowy stos bez weryfikacji... koszt bez gwarancji"). Fakt wsparty: 00#Gaps i 01 D1 (Unsupported) potwierdzają jedno źródło. Słabość: con spłaszcza dwie rzeczy. Ja nie bronię runtime'u, tylko zasady "limity i walidacja poza modelem", którą con sam przyznaje na końcu ("limity pętli poza modelem"). Teza 1 i sekcja "przyznaję" są więc częściowo sprzeczne.
2. **con#3** (no-progress guard nie obejmuje tur, intro nie poprawiono). **Poparte**: 01 C1 oraz bm-patterns-1-7 F5c ("it does not span turns"). Ale wniosek "to luka dotycząca nas najbardziej" jest interpretacją con. Źródło mówi, że tury pilnuje enum + `maxIterations`, a tego nasz kod ma trzymać sam. To argument za limitem w naszym kodzie, nie przeciw wzorcowi.
3. **con#7** (ryzyko pamięci: "STRONG (OWASP)"). **Przesadzone.** LSR#7 oznacza "Ryzyka" jako "moja ocena", a OWASP LLM04 dotyczy zatrucia danych i modelu, nie wprost pamięci użytkownika. Lista OWASP jest STRONG, jej zastosowanie do pamięci to MEDIUM.
4. **con#6** (AutoGen: rozbieżności proza/kod). **Poparte**: autogen-raw linia 549 ("no max iterations in code"), linia 199 ("not meant to be used in real applications"). Zgoda w całości.

## Krytyka prag

5. **prag ADOPT, limit pętli** ("zgodne: Anthropic, ReAct, Brad"). **Błędnie przypisane.** 01#2 podaje "blogi o ReAct", a LSR#8 cytuje warunki stopu z Anthropic; abstrakt ReAct ich nie zawiera. Dwa źródła, nie trzy.
6. **prag ADOPT, RAG + checki SKU/URL = "MEDIUM [README#9]"**. Rozdział 9 jest oznaczony "Średnia/Niska — blogi", a cytat o SKU/URL to punkt 8 tego rozdziału, nie dowód. Ocena MEDIUM jest życzliwa, ale prag trafnie umieszcza to w tierze ADOPT jako praktykę, nie wynik badań.
7. **prag ADOPT, "Zheng >80%" jako próg awansu**. Prag zaznacza, że próg to jego propozycja. LSR#2 mówi o ">80%" dla silnych sędziów (GPT-4) na MT-Bench, nie dla zbioru sklepu. Uczciwe oznaczenie, ale próg przenosi się słabo.
8. **prag ADOPT, idempotency/journal "MEDIUM [00#A]"**. Tu trafia też w mój claim 5, więc **przyznaję błąd własny**: napisałem "brak automatycznego ponowienia przy nieznanym wyniku". Źródło (bm-series-parts A6) mówi odwrotnie: "back off and try again with the SAME key". Poprawka: retry z tym samym kluczem idempotencji, bez nowego efektu.

## Zgoda (kandydaci na konsensus)
- Prompt caching prefiksu, backend-proxy i limity kosztu, brak uprawnień zapisu (LSR#6, #9) jako krok 1.
- Debata, quorum, aukcje, dynamic team, AutoGen jako kod produkcyjny: SKIP (01#2 "Low", brak danych skuteczności).
- Reflection/judge tylko z zewnętrznym sygnałem lub skalibrowany na etykietach ludzkich (Zheng, Huang; 3 niezależne grupy).

## Kwestie sporne
- Czy "deterministyczna powłoka" w naszym repo to wymóg nowej pracy (moje twierdzenie 5) czy już istniejące Outbox/Saga (prag), i czy czatbot read-only w ogóle potrzebuje journala w v1?
- Kiedy wprowadzać router intencji jako enum: od razu (ja) czy dopiero po pomiarze (con: "jedno wywołanie + RAG")? Żadne źródło nie mierzy zysku routera w czatbocie sklepu.
