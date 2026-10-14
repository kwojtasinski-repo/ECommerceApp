# Debater: con, Round 1, critique

Zweryfikowano w: 00-summary.md, 01-cross-source-verification.md, findings/bm-series-parts-raw, bm-patterns-8-13-raw, bm-agentic-core-raw, bm-agents-fsm, llm-self-research/README.md.

## Krytyka pro

**P1 (pro #1)**: "Kierunek potwierdzają niezależnie Zheng 2023 i Huang 2023", z oceną STRONG co do kierunku.
Zheng i Huang dotyczą oceniania i samokorekty, nie sterowania przepływem przez maszynę stanów. 01#4 D5 ocenia zdanie Brada o LLM jako maszynie stanów na "Partially supported", zgodne "na poziomie kierunku (nie dosłownie)". Ocena STRONG jest więc zawyżona. Przykład "potwierdzenie zamówienia bez weryfikacji" to pomysł pro, nie ze źródeł.

**P2 (pro #4)**: "Zheng, Huang i Brad zgadzają się, że sędzia jest słaby".
Przypisanie jest przekręcone. README#2 cytuje Zhenga: mocni sędziowie dają "over 80% agreement", czyli tyle co ludzie, z nazwanymi biasami. To nie "słaby sędzia". Huang dotyczy samokorekty bez zewnętrznego sygnału, nie sędziowania. "Weakest signal" jest tylko u Brada (01#2 zalicza to jednak do kierunkowego High). Pro zlewa trzy różne tezy w jedną.

**P3 (pro #2)**: limity pętli = STRONG, z "cap 128", "size(drafts)<4", "stalled".
Liczby wytrzymują sprawdzenie na poziomie cytatów: 128 jest dosłownie w bm-agentic-core B4, a `size(drafts) < 4` i `stalled` w bm-patterns-8-13. Pierwsza tura (bm-agents-fsm) miała 128 jako Low, ale została zastąpiona. Ocena STRONG jest jednak zawyżona: 01#2 mówi o niezależności "częściowo", a ReAct to "blogi". Mechanizmy 128 i `drafts<4` są specyficzne dla reducera, który pro sam odrzuca. Dla czatbota wystarczy `for` z licznikiem.

**P4 (pro, kolejność)**: "tydzień 1", "tygodnie 2-3".
Harmonogram nie pochodzi z żadnego źródła. Pro nie oznacza go jako własnej propozycji (prag to robi). "Wzorzec 9 z zewnętrznym sygnałem" to w rzeczywistości sprawdzanie SKU/URL z README#9 pkt 8, a nie mechanizm Brada. Brad ma gate recompute+diff (README#3).

Pro po sprawdzeniu przeżywa w części: caching (README#6) i UNKNOWN-not-FAILED (bm-series-parts A6) są zacytowane poprawnie, a pro uczciwie ocenia #5 i #8 jako WEAK/MEDIUM.

## Krytyka prag

**G1 (prag, ADOPT)**: limit pętli, "zgodne: Anthropic, ReAct, Brad. STRONG".
README#8 cytuje ReAct o halucynacjach i propagacji błędów, nie o limitach. Limity pochodzą z Anthropic. 01#2 pisze "częściowo" niezależne. ReAct został dodany bez oparcia w źródle.

**G2 (prag, tiering)**: "Deterministyczna powłoka" i "RAG po katalogu" w ADOPT NOW z oceną MEDIUM.
Jest to niespójne z własną regułą (wartość × dowody). LLM-as-judge ma tę samą ocenę MEDIUM, a trafia do PILOT. Powłoka Brada to jednoautorskie twierdzenie (01#2: brak korelacji niezależnej). RAG opiera się na README#9, który sam o sobie mówi "moja synteza", z blogów. "To już macie" pochodzi z kontekstu repo, nie ze źródeł.

**G3 (prag, SKIP)**: wzorce 6, 10, 12, 13, uzasadnienie "Low (brak dowodu skuteczności)" [01#2].
01#2 odnosi "Low" wyłącznie do debaty. Aukcja (10) i dynamiczny zespół (13) nie mają tam żadnej oceny skuteczności. Odrzucenie jest rozsądne, ale cytat tego nie wspiera.

**G4 (prag, PILOT)**: "Krótka pamięć sesji" nie ma żadnego cytowanego źródła.

**Samokrytyka (ustępstwo)**: moja teza 6 uogólnia "not meant to be used in real applications". Wg 01 C10 dotyczy to strony group chat, nie całego AutoGen. Wycofuję uogólnienie, zostają rozbieżności proza-kod C7-C12.

## Zgodność (kandydaci na konsensus)
1. Backend-proxy, klucz po stronie serwera, budżet kosztowy i `max_tokens`, bez uprawnień zapisu (OWASP LLM06/LLM10).
2. Prompt caching stałego prefiksu (README#6, dokumentacja).
3. Reflection i judge tylko z zewnętrznym sygnałem lub skalibrowane na etykietach ludzkich; debata, quorum, aukcja i pamięć długoterminowa odłożone.

## Sporne
1. Czy "deterministyczna powłoka Brada" (idempotencja, journal) wchodzi do ADOPT, skoro dowód jest jednoautorski i dotyczy akcji, których czatbot w MVP nie ma.
2. Czy ocena STRONG przysługuje "kierunkowej" zgodzie, gdy źródła niezależne (Zheng, Huang) dotyczą innego zagadnienia niż twierdzenie.
