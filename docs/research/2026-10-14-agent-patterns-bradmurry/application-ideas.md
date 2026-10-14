# Pomysły zastosowań: aplikacja ECommerceApp i setup Claude Code / Copilot

- **Data:** 2026-10-14
- **Status:** **hipotezy do przetestowania**, nie rekomendacje wdrożeniowe. Wynik rozmowy po researchu i debacie ([`debate/decision.md`](debate/decision.md)).
- **Co przejrzałem:** strukturę repo, [`docs/architecture/bounded-context-map.md`](../../architecture/bounded-context-map.md), foldery `.claude/` i `.github/`, `.github/AGENT-PIPELINE.md`, `.github/context-cost-analysis.md`, `tools/hooks/README.md`, `.github/instructions/{rag,agent-memory}.instructions.md`, `.claude/settings.json`.
- **Czego NIE przejrzałem:** kodu domenowego głębiej niż nazwy plików, wolumenu zamówień/zwrotów/zgłoszeń (brak danych), `mcp-routing-eval.prompt.md`, `tools/rag/queries.yaml`, treści pięciu folderów z agentami w korzeniu repo (`classifier`, `requirements-critic`, `linguistic-boundary-verifier`, `test-strategy-reviewer`, `transcriptor-criticor`, `maet-program-classifierator`).
- **Zasada przewodnia (z debaty):** LLM tam, gdzie wynik da się zmierzyć na własnych danych i gdzie człowiek lub deterministyczny check stoi za nim. Ścieżka transakcyjna (rezerwacje, płatności, sagi, naliczanie kuponów) zostaje bez LLM.

---

## A. Aplikacja ECommerceApp (rdzeń sklepu)

Dziś w repo nie ma integracji z LLM (sprawdzone przez pragmatyka debaty i potwierdzone przez użytkownika).

| # | Pomysł | Gdzie w repo | Co daje | Jak zmierzyć | Kryterium „stop" (propozycja) | Ryzyko |
|---|---|---|---|---|---|---|
| A1 | Porządkowanie katalogu przez admina: szkic opisu/kategorii/tagów z krótkiego wejścia, wykrywanie duplikatów produktów | `Catalog/Products`, `Backoffice` | oszczędność ręcznej pracy admina; admin zatwierdza | odsetek szkiców przyjętych bez zmian, minuty na produkt | przyjęte bez zmian < ok. połowy na próbce 50–100 produktów | niskie (człowiek w pętli) |
| A2 | Diagnoza „czemu to zamówienie utknęło": LLM czyta stan sagi, outbox i historię płatności (tylko odczyt) i tłumaczy po ludzku | `Sagas`, `Messaging`, `Payments` | najboleśniejsze miejsce systemu asynchronicznego; narzędzie dla dev/ops | zgodność diagnozy ze znaną przyczyną na sztucznych przypadkach z testów E2E | diagnoza trafna w mniej niż większości znanych przypadków | niskie (tylko odczyt, wewnętrznie) |
| A3 | Klasyfikacja powodów zwrotów/anulowań do stałej taksonomii + dashboard | `Fulfillment/Refund` (ma `Reason`), `OrderCancelledPayload` | widać, dlaczego klienci zwracają | zgodność z ręcznie oznaczonymi ok. 100 przypadkami | zgodność niska lub wolumen zbyt mały, by dashboard coś pokazał | niskie; wartość zależy od wolumenu |
| A4 | Wyszukiwanie semantyczne po katalogu | Catalog + wyszukiwarka | mniej zapytań bez wyniku | odsetek wyszukań bez wyniku i kliknięć | brak poprawy względem obecnej wyszukiwarki | średnie (infrastruktura embeddingów) |
| A5 | Szkic reguły kuponu z opisu słownego, walidowany istniejącym schematem parametrów | `Sales/Coupons` | szybsze tworzenie reguł | odsetek poprawnych reguł na zestawie fraz | admin i tak szybciej w istniejącym UI | niskie, zysk raczej mały |
| A6 | Czatbot klienta (FAQ, polityka zwrotów, status zamówienia tylko do odczytu) | nowy moduł | odciążenie supportu, o ile jest ruch | odsetek rozmów rozwiązanych bez człowieka | brak ruchu/zgłoszeń do odciążenia | najwyższe (ruch publiczny) |

**Poza LLM:** rezerwacje zapasu, płatności, sagi, naliczanie kuponów. Powód: Wasze zasady (saga bez blokad, natychmiastowy status zamówienia) i debata (SKIP/DEFER dla akcji z efektem).

**Metoda zanim cokolwiek powstanie:** 1–2 dni na teście offline: eksport ok. 100 rekordów, przepuszczenie przez model, policzenie zgodności z ręcznymi etykietami. Słabo → koniec, zanim powstanie kod.

---

## B. Setup Claude Code / GitHub Copilot (to, jak pracujecie nad repo)

### Co już macie i jak się to ma do researchu
Dane z repo, nie z mojej pamięci:

| Wzorzec z researchu | Co macie w repo |
|---|---|
| Deterministyczna powłoka, legalność strukturalna | GO gate: hooki `UserPromptSubmit` i `PreToolUse` blokują `Edit/Write/NotebookEdit` do czasu `IMPLEMENT` (`.claude/settings.json`, `tools/hooks/`, ADR-0033) |
| Człowiek w pętli, jawne limity pętli | `AGENT-PIPELINE.md`: planner max-iter 3, implementer max 5 przebiegów, 3 próby weryfikacji (build + unit + integration), `bc-switch` max-iter 10, dwa punkty HITL |
| Zewnętrzny sygnał zamiast samokrytyki (reflection) | embedded `@verifier`: build + testy jako deterministyczny gate |
| Journal / wznawianie po restarcie | `session-continuity.instructions.md`: numerowane pliki faz plan/walidacja |
| RAG nad dokumentacją + graf wiedzy | `tools/rag` (Qdrant, MCP), `tools/kg` (Neo4j, ADR-0031) |
| Routing modeli | `context-cost-analysis.md` §7: tańsze (included) modele do eksploracji, droższy do implementacji; gatherery `research-gather` na tańszym modelu |
| Pamięć między sesjami | `.github/context/agent-decisions.md` + pamięć Claude Code |
| Walidator z innej rodziny modeli | z Waszych notatek wynika praktyka: implementacja jednym modelem, niezależna walidacja innym |

Wniosek: **jesteście z przodu względem większości tego, co opisują źródła.** Poniżej tylko luki, nie „dodajmy wszystko".

### Pomysły (kolejność = moja ocena wartości do kosztu)

| # | Pomysł | Dlaczego | Jak zmierzyć | Kryterium „stop" (propozycja) |
|---|---|---|---|---|
| B1 | **Zmierzyć, nie szacować**: `context-cost-analysis.md` opiera się na założeniach (4 znaki = 1 token, założony hit rate cache'u, stan z 2026-06-03). Zrobić pomiar rzeczywisty w Claude Code (`/context`, `/cost`) i w Copilot (jeśli panel pokazuje) | bez pomiaru nie wiadomo, czy 7 110 tokenów stałego bloku i „warm turn" to prawda | tokeny stałego bloku i koszt typowej sesji, przed i po | pomiar zgodny z szacunkiem ±15% → nic nie zmieniać |
| B2 | **Odchudzanie stałego bloku instrukcji metodą Brada** (jedna zmiana naraz, pomiar, raport sufitu): największy to `mcp-routing.instructions.md` 2 058 tokenów, potem `copilot-instructions.md` 2 184; cel: ta sama jakość routingu przy mniejszej liczbie tokenów. Wymaga małego zestawu „złotych zadań" z checkami deterministycznymi (czy agent użył RAG zamiast czytać cały plik? czy przeczytał `agent-decisions.md`?) | to dokładnie „prompt optimization" w skali, w której macie dane | tokeny/turę i odsetek zdanych złotych zadań | spadek jakości na złotych zadaniach → wycofać zmianę |
| B3 | **Stabilny prefiks = tańsza sesja**: wg dokumentacji API zmiana definicji narzędzi lub systemowego prefiksu unieważnia cache dalszych poziomów (tools → system → messages). W praktyce: nie edytować plików ładowanych zawsze i nie dodawać/odłączać serwerów MCP w środku drogiej sesji. W tej sesji `ecommerceapp-kg` nie połączył się, więc zestaw narzędzi był inny niż zwykle. **Uwaga:** to wynika z dokumentacji API Anthropic, nie sprawdziłem, jak dokładnie Claude Code buforuje | tańsze długie sesje | porównanie kosztu sesji z/bez zmian narzędzi w trakcie | brak różnicy w pomiarze → odpuścić |
| B4 | **Dziennik zdarzeń z hooków** (append-only JSONL): dodać hook `PostToolUse`, który zapisuje narzędzie, czas i wynik. Dziś macie hooki tylko na `UserPromptSubmit` i `PreToolUse`. Pozwala odtworzyć, co agent faktycznie zrobił, gdy relacja agenta rozjeżdża się z rzeczywistością (Wasze notatki zawierają przypadek: regresja logowania opisana jako „pre-existing") | „The run is the debug artifact" (Brad); post-mortem na faktach | czy dziennik rozstrzyga spór „co agent zrobił" w kolejnym incydencie | brak użycia po miesiącu → wyłączyć |
| B5 | **Policzyć, ile daje niezależna walidacja, na własnych danych**: z notatek faz (plan/walidacja) zrobić tabelę „co walidator znalazł, a implementer przeoczył" (np. brakujące testy, błąd POST płatności gościa, regresja logowania). To Wasz własny „kalibrowany sędzia" i mocniejszy dowód niż paper z 2023 | Zheng i in. mierzyli czat, nie Wasz workflow | odsetek faz, w których walidator znalazł realny błąd | jeśli walidator rzadko coś znajduje → rozważyć tańszą walidację |
| B6 | **Debata multi-agent tylko dla spornych ADR**: np. wciąż odroczona decyzja o zakresie usunięcia martwego kodu ADR-0026. Reguły z naszej debaty: różne perspektywy, wspólna baza dowodów, krytyki z weryfikacją źródeł, sędzia z enumem i limitem rund | koszt debaty jest znany; wartość tylko tam, gdzie są realne alternatywy | czy debata ujawniła kwestię, której zwykły plan nie wykrył | jeśli nic nowego → nie powtarzać |
| B7 | **Parytet agentów Claude Code z Copilot**: w `.github/agents/` jest 19 agentów (`planner`, `implementer`, `research-planner`…), a w `.claude/agents/` tylko 2 (`assumption-check`, `what-if`). Skill `research-gather` użyty w tej sesji pochodzi z repo Gradebook (dodatkowy katalog roboczy). Opcja: lekkie odpowiedniki w `.claude/agents/` albo świadoma decyzja, że Claude Code korzysta z instrukcji `.github/` przez `CLAUDE.md` | mniej zależności od innego repo | czy research/plan da się uruchomić bez Gradebooka z tą samą jakością | jeśli rzadko używane → nie robić |
| B8 | **Konsolidacja pamięci**: `agent-decisions.md` rośnie. Okresowo scalać wpisy epizodyczne w reguły i usuwać nieaktualne (pamięć o plikach/funkcjach weryfikować przed użyciem). Macie już `ac-knowledge-review.prompt.md` — prawdopodobnie to pokrywa; nie sprawdzałem | pamięć długoterminowa bez pielęgnacji dryfuje | liczba wpisów i odsetek nadal prawdziwych | jeśli prompt już to robi → nic |

### Czego bym w setupie NIE dodawał
- Własnego runtime'u reducera (Aegis) ani frameworku multi-agent: macie już HITL, limity i gate; źródła nie dają niezależnego dowodu na przewagę.
- Automatycznych optymalizatorów promptów (GEPA/DSPy) przed istnieniem zestawu złotych zadań (B2).
- Debaty jako stałego kroku każdej zmiany: koszt rośnie z liczbą debaterów i rund, a w źródłach nie ma danych o skuteczności.

## C. Co z tym dalej (propozycja kolejności)
1. A2 i B1 (oba tanie i mierzalne), potem A1/A3 jako test offline.
2. B2 po B1 (potrzebny pomiar bazowy).
3. B5 jako analiza istniejących danych, bez budowania czegokolwiek.
4. Czatbot (A6) dopiero przy realnym ruchu.
