# Wyniki symulacji doboru wzorca (proxy z git)

Commity: 200 (od 2026-05-27 do 2026-10-14). Skrypt: `scripts/select_pattern.py`.

**To jest test spójności i odporności reguły na przybliżonych cechach, nie test trafności doboru.**

## 1. Rozkład rekomendacji (progi bazowe: {'S_f': 2, 'S_l': 60, 'L_f': 12, 'L_l': 800, 'L_p': 4, 'A_thr': 4, 'share': 0.6, 'U_add': 0.4, 'U_files': 8, 'risk_mode': 'broad'})

| Wzorzec | Commity | Udział |
|---|---|---|
| SEQUENTIAL_GATES | 133 | 66% |
| INDEPENDENT_VALIDATION | 85 | 42% |
| NONE | 67 | 34% |
| FANOUT_DECOMP_CHECK | 60 | 30% |
| ADD_DETERMINISTIC_CHECK_FIRST | 27 | 14% |
| PLANNER_EXECUTOR | 12 | 6% |
| DEBATE_CANDIDATE_ASK | 10 | 5% |

| Poziom autonomii | Commity | Udział |
|---|---|---|
| brak wzorca | 67 | 34% |
| A | 46 | 23% |
| B | 77 | 38% |
| C | 10 | 5% |

Rozmiar (S/M/L): {'L': 84, 'M': 95, 'S': 21}

## 2. Odporność na zmianę progów (300 losowych kombinacji progów, ziarno stałe)

- Średni odsetek kombinacji, w których rekomendacja commita jest identyczna z bazową: **90%**
- Commity ze stabilną rekomendacją (identyczna w ≥ 90% kombinacji): **70%**
- Commity niestabilne (identyczna w < 60% kombinacji): **6%**
- Średni odsetek kombinacji ze zmianą poziomu autonomii: **5%**

| Wzorzec | Zmiany (obecność/nieobecność) łącznie |
|---|---|
| FANOUT_DECOMP_CHECK | 3835 |
| NONE | 2065 |
| SEQUENTIAL_GATES | 2065 |
| PLANNER_EXECUTOR | 765 |
| DEBATE_CANDIDATE_ASK | 732 |
| ADD_DETERMINISTIC_CHECK_FIRST | 222 |
| INDEPENDENT_VALIDATION | 222 |

## 3. Arkusz do oznaczenia (20 ostatnich commitów M/L, nie dokumentacja/testy-only)

Kolumna „Zgoda?” jest pusta: wpisz T/N, jeśli proponowany wzorzec miałby sens. Propozycja pochodzi z reguły i proxy z git, a **nie** z oceny agenta.

| # | Commit | Data | Temat | Pliki / linie / obszary | Proponowane | Poziom | Zgoda? |
|---|---|---|---|---|---|---|---|
| 1 | `8a768281` | 2026-10-14 | feat(agents): add agent discipline pack and GO-gate hooks (ADR-0033) | 29 / 1286 / 7 | ADD_DETERMINISTIC_CHECK_FIRST, DEBATE_CANDIDATE_ASK, FANOUT_DECOMP_CHECK, INDEPENDENT_VALIDATION, PLANNER_EXECUTOR, SEQUENTIAL_GATES | C | |
| 2 | `a907f475` | 2026-09-03 | fix(orders,presale,messaging): stop showing success before stock/statu | 15 / 312 / 7 | FANOUT_DECOMP_CHECK, INDEPENDENT_VALIDATION, SEQUENTIAL_GATES | B | |
| 3 | `78ba1f27` | 2026-09-03 | test(web-e2e): run ECommerceApp.Web.E2E against a real SQL Server cont | 14 / 319 / 5 | FANOUT_DECOMP_CHECK, SEQUENTIAL_GATES | A | |
| 4 | `ca0c22eb` | 2026-09-03 | fix(catalog,messaging,coupons): fix EF Core translation and resilience | 7 / 147 / 5 | ADD_DETERMINISTIC_CHECK_FIRST, INDEPENDENT_VALIDATION, SEQUENTIAL_GATES | B | |
| 5 | `e2fb3048` | 2026-09-03 | fix(coupons): reject invalid scope targets as validation failures, not | 3 / 58 / 3 | INDEPENDENT_VALIDATION, SEQUENTIAL_GATES | B | |
| 6 | `070abd26` | 2026-09-02 | feat(coupons): let admins edit coupon rules, closing the rule-builder  | 34 / 961 / 13 | FANOUT_DECOMP_CHECK, INDEPENDENT_VALIDATION, SEQUENTIAL_GATES | B | |
| 7 | `e8caa6ca` | 2026-09-02 | build: repair the solution file and register project docs as solution  | 1 / 101 / 1 | SEQUENTIAL_GATES | A | |
| 8 | `c6b04289` | 2026-09-02 | feat(coupons): replace the raw-JSON rules textarea with a generated ru | 16 / 805 / 6 | DEBATE_CANDIDATE_ASK, FANOUT_DECOMP_CHECK, INDEPENDENT_VALIDATION, PLANNER_EXECUTOR, SEQUENTIAL_GATES | C | |
| 9 | `1a11ed59` | 2026-09-02 | fix: order by typed-id value so paged queries work on the InMemory pro | 3 / 12 / 3 | SEQUENTIAL_GATES | A | |
| 10 | `60bffcaa` | 2026-09-02 | feat(coupons): add rule parameter schema, catalog and create-time vali | 31 / 732 / 4 | INDEPENDENT_VALIDATION, SEQUENTIAL_GATES | B | |
| 11 | `62b85843` | 2026-08-26 | docs: align solution index with repository structure | 5 / 334 / 3 | ADD_DETERMINISTIC_CHECK_FIRST, INDEPENDENT_VALIDATION, SEQUENTIAL_GATES | B | |
| 12 | `2b16db64` | 2026-08-26 | chore: sync solution docs and remove retired context mode | 1 / 218 / 1 | SEQUENTIAL_GATES | A | |
| 13 | `1a78497c` | 2026-08-26 | docs(adr): complete F1 slimming rollout across Bucket A, fix ADR-0011  | 45 / 5677 / 3 | ADD_DETERMINISTIC_CHECK_FIRST, INDEPENDENT_VALIDATION, SEQUENTIAL_GATES | B | |
| 14 | `68200ff4` | 2026-08-25 | [ECommerceApp] UI Improvements, fixed bugs | 22 / 648 / 8 | FANOUT_DECOMP_CHECK, INDEPENDENT_VALIDATION, SEQUENTIAL_GATES | B | |
| 15 | `0fba207e` | 2026-08-25 | test: add message TestData builder family, dedupe construction across  | 17 / 376 / 5 | FANOUT_DECOMP_CHECK, INDEPENDENT_VALIDATION, SEQUENTIAL_GATES | B | |
| 16 | `b36fe5be` | 2026-08-25 | fix(sales): remove dead OrderItemsController.ByItem action | 3 / 15 / 2 | SEQUENTIAL_GATES | A | |
| 17 | `2a1648c1` | 2026-08-24 | refactor(test): dedupe per-handler-scope ModuleClient test double, fix | 4 / 164 / 3 | INDEPENDENT_VALIDATION, SEQUENTIAL_GATES | B | |
| 18 | `16fae7a2` | 2026-08-24 | fix(inventory): stop nested transactions across saga/shipment handlers | 27 / 1313 / 11 | FANOUT_DECOMP_CHECK, INDEPENDENT_VALIDATION, SEQUENTIAL_GATES | B | |
| 19 | `b6cf9b28` | 2026-08-20 | fix(test): stabilize outbox polling and hosted shutdown | 19 / 1008 / 11 | FANOUT_DECOMP_CHECK, INDEPENDENT_VALIDATION, SEQUENTIAL_GATES | B | |
| 20 | `846d10a3` | 2026-08-20 | docs: mark context-mode ADR-0029 retired with rationale, fix stale roa | 12 / 105 / 3 | SEQUENTIAL_GATES | A | |

## 4. Test brzegowy: małe zadania (S)

- Commity S: 21; z nich z rekomendowanym wzorcem (powinno być tylko przy wysokim ryzyku): **0**

## 5. Diagnostyka: co uruchamia „wysokie ryzyko” w wariancie szerokim

| Wyzwalacz | Commity |
|---|---|
| nproj>=3 | 81 |
| msg | 22 |
| saga | 10 |
| mig | 7 |

## 6. Wariant z węższym ryzykiem (migracje, sagi, ≥ 4 projekty): jedna zmiana względem bazy

| Wzorzec | Baza | Wariant |
|---|---|---|
| SEQUENTIAL_GATES | 133 (66%) | 133 (66%) |
| INDEPENDENT_VALIDATION | 85 (42%) | 64 (32%) |
| ADD_DETERMINISTIC_CHECK_FIRST | 27 (14%) | 19 (10%) |
| FANOUT_DECOMP_CHECK | 60 (30%) | 60 (30%) |
| PLANNER_EXECUTOR | 12 (6%) | 12 (6%) |
| DEBATE_CANDIDATE_ASK | 10 (5%) | 9 (4%) |
| NONE | 67 (34%) | 67 (34%) |

| Poziom | Baza | Wariant |
|---|---|---|
| brak | 67 (34%) | 67 (34%) |
| A | 46 (23%) | 66 (33%) |
| B | 77 (38%) | 58 (29%) |
| C | 10 (5%) | 9 (4%) |

## 7. Wariant 2: ryzyko = migracje, sagi lub ≥ 3 projekty ŹRÓDŁOWE (bez docs, testów i .github)

Przyczyna: w wariancie bazowym „≥ 3 projekty” liczyło też `docs/` i projekty testowe jako osobne projekty.

| Wzorzec | Baza | Wariant 2 |
|---|---|---|
| SEQUENTIAL_GATES | 133 (66%) | 133 (66%) |
| INDEPENDENT_VALIDATION | 85 (42%) | 41 (20%) |
| ADD_DETERMINISTIC_CHECK_FIRST | 27 (14%) | 13 (6%) |
| FANOUT_DECOMP_CHECK | 60 (30%) | 60 (30%) |
| PLANNER_EXECUTOR | 12 (6%) | 12 (6%) |
| DEBATE_CANDIDATE_ASK | 10 (5%) | 7 (4%) |
| NONE | 67 (34%) | 67 (34%) |

| Poziom | Baza | Wariant 2 |
|---|---|---|
| brak | 67 (34%) | 67 (34%) |
| A | 46 (23%) | 87 (44%) |
| B | 77 (38%) | 39 (20%) |
| C | 10 (5%) | 7 (4%) |
