# Research Brief: Wzorce budowy agentów AI (Brad Murry + Medium/Mastra/AutoGen) i ich użycie na produkcji

## Research Question
Zebrać i zweryfikować krzyżowo treść 18 podanych źródeł (14 postów bradmurry.com/software, artykuł Medium
"12 design patterns", Mastra "Patterns of Building AI Agents", 2 strony AutoGen: multi-agent-debate i
design-patterns intro). Docelowo (poza tym workflow): na bazie zebranych faktów przeprowadzić pattern
multi-agent debate z kilkoma agentami — jakie umiejętności/wzorce da się wykorzystać w AI na produkcji.

## Type
external
Self-check performed: pytanie wymaga wyłącznie wiedzy spoza organizacji (podane URL-e), brak źródeł
wewnętrznych (kod/ticket/transkrypt) do odpytania. Odniesienie do ECommerceApp jest tylko kontekstem
późniejszego kroku, nie źródłem tej fazy. Klasyfikacja prosta.

## Scope

### In Scope
- Wzorce, architektury i umiejętności agentowe opisane w 18 podanych URL-ach
- Reactive reducer pattern, FSM + LLM, human-in-the-loop, self-evolving agents, agents in production
- Multi-agent debate (AutoGen) — mechanika wzorca
- Wskazówki produkcyjne (niezawodność, kontrakty, konfiguracja, skala)

### Out of Scope
- Synteza, rekomendacja i końcowy raport (osobny krok po tym workflow)
- Samo przeprowadzenie debaty agentów (osobny krok po tym workflow)
- Źródła spoza podanej listy (poza ewentualną weryfikacją podstawowych faktów)

### Source Restrictions
- Tylko 18 podanych URL-i; każdy fakt musi mieć URL + cytat

## Success Criteria

### Concrete Deliverables
- [ ] Per źródło: lista wzorców/pojęć/umiejętności z cytatami i confidence
- [ ] Katalog wzorców z deduplikacją między źródłami (np. HITL, FSM, reflection)
- [ ] Opis mechaniki multi-agent debate (role, rundy, agregacja) z AutoGen
- [ ] Lista wzorców oznaczonych jako nadających się na produkcję (z uzasadnieniem ze źródeł)
- [ ] Kontradykcje i luki między źródłami

### Information Quality Requirements
- Każdy finding z URL i cytatem; brak faktów z pamięci treningowej; niedostępne strony logowane jako luka

## Actors
None — research is actor-agnostic

## Information Layers

### Big Picture
- Jakie klasy problemów rozwiązują te wzorce, czemu agenci zawodzą na produkcji

### Modeling
- FSM, reducer, stany, kontrakty, partycjonowanie DSL/AI

### Implementation
- Konkretne frameworki (AutoGen, Mastra, Flink, sparkctl), konfiguracja, API

## Output Location
docs/research/2026-10-14-agent-patterns-bradmurry/ — potwierdzone przez użytkownika (AskUserQuestion, 2026-10-14)
