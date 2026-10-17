# Pattern selection simulation results (git proxies)

Commits: 200 (from 2026-05-27 to 2026-10-14). Script: `scripts/select_pattern.py`.

**This is a test of the rule's consistency and robustness on approximate features, not a test of selection accuracy.**

## 1. Distribution of recommendations (base thresholds: {'S_f': 2, 'S_l': 60, 'L_f': 12, 'L_l': 800, 'L_p': 4, 'A_thr': 4, 'share': 0.6, 'U_add': 0.4, 'U_files': 8, 'risk_mode': 'broad'})

| Pattern | Commits | Share |
|---|---|---|
| SEQUENTIAL_GATES | 133 | 66% |
| INDEPENDENT_VALIDATION | 85 | 42% |
| NONE | 67 | 34% |
| FANOUT_DECOMP_CHECK | 60 | 30% |
| ADD_DETERMINISTIC_CHECK_FIRST | 27 | 14% |
| PLANNER_EXECUTOR | 12 | 6% |
| DEBATE_CANDIDATE_ASK | 10 | 5% |

| Autonomy level | Commits | Share |
|---|---|---|
| no pattern | 67 | 34% |
| A | 46 | 23% |
| B | 77 | 38% |
| C | 10 | 5% |

Size (S/M/L): {'L': 84, 'M': 95, 'S': 21}

## 2. Robustness to threshold changes (300 random threshold combinations, fixed seed)

- Mean share of combinations in which a commit's recommendation is identical to the base one: **90%**
- Commits with a stable recommendation (identical in ≥ 90% of combinations): **70%**
- Unstable commits (identical in < 60% of combinations): **6%**
- Mean share of combinations with a change of autonomy level: **5%**

| Pattern | Changes (presence/absence) in total |
|---|---|
| FANOUT_DECOMP_CHECK | 3835 |
| NONE | 2065 |
| SEQUENTIAL_GATES | 2065 |
| PLANNER_EXECUTOR | 765 |
| DEBATE_CANDIDATE_ASK | 732 |
| ADD_DETERMINISTIC_CHECK_FIRST | 222 |
| INDEPENDENT_VALIDATION | 222 |

## 3. Sheet to label (the 20 latest M/L commits, not docs/tests-only)

The "Agree?" column is empty: enter Y/N if the proposed pattern would make sense. The proposal comes from the rule and git proxies, **not** from an agent's assessment.

| # | Commit | Date | Subject | Files / lines / areas | Proposed | Level | Agree? |
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

## 4. Edge test: small tasks (S)

- S commits: 21; of which with a recommended pattern (should occur only at high risk): **0**

## 5. Diagnostics: what triggers "high risk" in the broad variant

| Trigger | Commits |
|---|---|
| nproj>=3 | 81 |
| msg | 22 |
| saga | 10 |
| mig | 7 |

## 6. Variant with narrower risk (migrations, sagas, ≥ 4 projects): one change relative to the base

| Pattern | Base | Variant |
|---|---|---|
| SEQUENTIAL_GATES | 133 (66%) | 133 (66%) |
| INDEPENDENT_VALIDATION | 85 (42%) | 64 (32%) |
| ADD_DETERMINISTIC_CHECK_FIRST | 27 (14%) | 19 (10%) |
| FANOUT_DECOMP_CHECK | 60 (30%) | 60 (30%) |
| PLANNER_EXECUTOR | 12 (6%) | 12 (6%) |
| DEBATE_CANDIDATE_ASK | 10 (5%) | 9 (4%) |
| NONE | 67 (34%) | 67 (34%) |

| Level | Base | Variant |
|---|---|---|
| none | 67 (34%) | 67 (34%) |
| A | 46 (23%) | 66 (33%) |
| B | 77 (38%) | 58 (29%) |
| C | 10 (5%) | 9 (4%) |

## 7. Variant 2: risk = migrations, sagas or ≥ 3 SOURCE projects (excluding docs, tests and .github)

Reason: in the base variant "≥ 3 projects" also counted `docs/` and test projects as separate projects.

| Pattern | Base | Variant 2 |
|---|---|---|
| SEQUENTIAL_GATES | 133 (66%) | 133 (66%) |
| INDEPENDENT_VALIDATION | 85 (42%) | 41 (20%) |
| ADD_DETERMINISTIC_CHECK_FIRST | 27 (14%) | 13 (6%) |
| FANOUT_DECOMP_CHECK | 60 (30%) | 60 (30%) |
| PLANNER_EXECUTOR | 12 (6%) | 12 (6%) |
| DEBATE_CANDIDATE_ASK | 10 (5%) | 7 (4%) |
| NONE | 67 (34%) | 67 (34%) |

| Level | Base | Variant 2 |
|---|---|---|
| none | 67 (34%) | 67 (34%) |
| A | 46 (23%) | 87 (44%) |
| B | 77 (38%) | 39 (20%) |
| C | 10 (5%) | 7 (4%) |
