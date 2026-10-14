# Rejected Information (Phase 3, step 3 — skonsolidowane)

Data: 2026-10-14 · Pytanie: wzorce/umiejętności agentowe z podanych źródeł (bez rekomendacji) · Zakres: 18 URL-i + rozszerzenie (13 artykułów per wzorzec, serie Self-evolving/AiP, 7 dodatkowych stron AutoGen).

## Poza zakresem (inny moduł/komponent)

| # | Informacja | Źródło | Dlaczego odrzucone | Re-include, jeśli |
|---|---|---|---|---|
| R1 | Pełne YAML-e incident-triage powtórzone w AiP 3 i 4 | `28`, `29` | duplikat YAML z `27` (zastąpiony odsyłaczem) | potrzebny diff między wersjami |
| R2 | Szczegóły domenowe przykładów (logi 40 MB, tickety, rekordy klientów) | `22`–`25` | konkretna domena, nie wzorzec | analiza samego benchmarku |
| R3 | Konfiguracja PyFlink (`make fresh`, venv), sprzęt DGX Spark | `45`, `42` | szczegóły środowiska, nie wzorce agentowe | wdrażanie tej infrastruktury |
| R4 | Narracyjne otwarcia/„Until next time", teasery | wszystkie posty | brak treści analitycznej | analiza stylu/tonu |
| R5 | Spłaszczone diagramy Mermaid i sekwencyjne | wklejki | artefakt kopiowania, brak informacji | dostęp do oryginalnych diagramów |
| R6 | Wynik wygenerowanej opowieści w group-chat | `36` | tekst generowany, nie wzorzec | ewaluacja jakości wyjścia |
| R7 | Linki wychodzące (arXiv 2406.11776 sparse topology, repo aegis, tool-use pages) | `39`, `03` | spoza listy URL; nie pobrane | rozszerzenie zakresu o weryfikację zewnętrzną skuteczności debaty |

## Rozwiązanie zamiast problemu / opinia

| # | Informacja | Źródło | Dlaczego odrzucone | Re-include, jeśli |
|---|---|---|---|---|
| R8 | „Outcome" apis-at-massive-scale („didn't even break a sweat") | `30` | samoopis bez liczb | pojawią się dane ruchu |
| R9 | Liczby „90% fast track", „80% of use case" | `43` | niepoparte | autor poda metodologię |
| R10 | Paraphrazowana lista 12 wzorców z lustra Medium | gatherer `medium` | niezweryfikowana domena, parafraza | użytkownik poda tekst artykułu |

## Luki zamiast odrzuceń
Medium (HTTP 403), Mastra (treść rozdziałów za bramką e-mail), AiP Parts 5–7 — patrz `01-cross-source-verification.md` §3.

## Podsumowanie
- Findings w plikach `*-raw`: ok. 490 sekcji `##` (część to sekcje pomocnicze), 1 448 zacytowanych fragmentów, 94,8% zgodnych dosłownie z plikami źródłowymi.
- Odrzucone grupy: ~10 (w każdym pliku findings od 5 do 9 wierszy tabeli „Rejected Information").
- Nic nie zostało odrzucone jako „nieprawdziwe" — odrzucenia dotyczą zakresu i niepoparcia.
