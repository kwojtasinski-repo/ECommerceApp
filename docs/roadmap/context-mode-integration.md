# Roadmap: context-mode — integracja sandboxa MCP

> Status: — Unblocked — RAG stabilisation complete (2026-05-23), ready to implement
> Scope: `docker/context-mode/`, `Dockerfile-context-mode`, `docker-compose.yaml` (delta), `.vscode/`, `.github/hooks/`, `.github/copilot-instructions.md` (append)
> Powiązany plan szczegółowy: [`context-mode-details.md`](./context-mode-details.md)

---

## Po co to robimy

GitHub Copilot wprowadza limity requestów (request usage billing). Każde wywołanie
narzędzia wrzuca surowe dane do okna kontekstu — co przekłada się bezpośrednio
na liczbę requestów do modelu.

| Narzędzie | Raw output | W kontekście po integracji | Redukcja |
|---|---|---|---|
| Playwright snapshot | 56 KB | 299 B | 99% |
| GitHub issues (20) | 59 KB | 1.1 KB | 98% |
| Logi testów (30 suite) | 6 KB | 337 B | 95% |
| Repo research (subagent) | 986 KB | 62 KB | 94% |
| **Cała sesja** | **315 KB** | **5.4 KB** | **98%** |

Cel: wydłużenie sesji roboczej z ~30 min do ~3 godzin bez utraty kontekstu.

---

## Założenia

| Nr | Założenie | Uzasadnienie |
|---|---|---|
| A1 | Build ze źródła (git clone pinowany tag), nie `npm install -g` | Pełna kontrola co jest uruchamiane; audytowalny kod |
| A2 | Docker — bez instalacji na hoście dla teamu | `docker compose up -d context-mode` to jedyna operacja |
| A3 | Sieć: `--network none` | Cross-runtime (Docker Desktop, Rancher, Podman); zero egress |
| A4 | Node.js network monitoring hook | Loguje próby połączeń; działa bez kernel capabilities |
| A5 | Alert log: `/workspace/.ctx-network-alerts.log` | Cross-runtime; widoczny w VS Code |
| A6 | VS Code Problem Matcher task | Alerty w panelu Problems; cross-runtime |
| A7 | Dozzle jako monitoring web UI | Zero konfiguracji; uruchamia się profilem `--profile monitoring` |
| A8 | Hooks przez `docker exec` (nie globalny CLI) | Team nie instaluje nic lokalnie |
| A9 | copilot-instructions.md: tylko append sekcji 13 | Istniejąca konfiguracja agentów nienaruszona |
| A10 | RAG MCP pozostaje aktywny obok context-mode | Każdy serwis do innego celu; brak konfliktu |
| A11 | Wersja pinowana w Dockerfile (v1.0.146) | Kontrolowany upgrade przez jeden commit |
| A12 | Non-root user w kontenerze | Hardening bezpieczeństwa |

---

## Czego się obawiamy — ryzyka i mitigacje

| Ryzyko | P-stwo | Wpływ | Mitigacja |
|---|---|---|---|
| context-mode exfiltruje dane przez sieć | Niskie | Wysoki | `--network none` blokuje wszystko; hook loguje próby |
| Breaking change w nowej wersji | Niskie | Średni | Pinowana wersja w Dockerfile; świadomy upgrade przez cały team |
| Hooks interferują z RAG MCP (`mcp__*` calls) | Niskie | Niski | `CONTEXT_MODE_EXTERNAL_MCP_NUDGE_EVERY=50` |
| copilot-instructions.md konflikt | Bardzo niskie | Wysoki | Append only; nowa sekcja 13; stara konfiguracja bez zmian |
| Różnice między Docker Desktop / Rancher / Podman | Możliwe | Niski | `--network none` działa identycznie; `docker`→`podman` podmiana lokalna |
| SQLite session DB nie persystuje | Brak | Wysoki | Named volume `context-mode-data` |
| Non-root user nie ma dostępu do workspace | Możliwe | Średni | Volume mount z właściwymi uprawnieniami; weryfikacja po pierwszym uruchomieniu |

---

## Fazy implementacji

### Faza 1 — Docker sandbox (fundament)

| Krok | Opis | Plik | Status |
|---|---|---|---|
| 1.1 | Node.js network monitoring hook | `docker/context-mode/network-monitor.js` | 🔲 |
| 1.2 | Entrypoint wrapper | `docker/context-mode/entrypoint.sh` | 🔲 |
| 1.3 | Dockerfile 2-stage (git clone → runtime) | `Dockerfile-context-mode` | 🔲 |
| 1.4 | docker-compose delta (serwis + volume + env) | `docker-compose.yaml` | 🔲 |
| 1.5 | Named volume dla SQLite session DB | `docker-compose.yaml` (volumes sekcja) | 🔲 |
| 1.6 | VS Code task: `Context-Mode: Start` | `.vscode/tasks.json` | 🔲 |
| 1.7 | Weryfikacja: `docker compose up -d context-mode` | terminal | 🔲 |

**Kryterium akceptacji Fazy 1**: kontener startuje, nie crasha, `docker logs ecommerceapp-context-mode` nie pokazuje błędów.

---

### Faza 2 — MCP connection do VS Code

| Krok | Opis | Plik | Status |
|---|---|---|---|
| 2.1 | MCP config (docker exec stdio) | `.vscode/mcp.json` | 🔲 |
| 2.2 | VS Code task: `Context-Mode: Stop`, `Context-Mode: Network Alerts` | `.vscode/tasks.json` | 🔲 |
| 2.3 | Weryfikacja: `ctx stats` w Copilot Chat | Copilot Chat | 🔲 |
| 2.4 | Test sandbox: `ctx_execute("javascript", "console.log('hello')")` | Copilot Chat | 🔲 |
| 2.5 | Dodanie `.ctx-network-alerts.log` do `.gitignore` | `.gitignore` | 🔲 |

**Kryterium akceptacji Fazy 2**: `ctx stats` zwraca odpowiedź; `ctx_execute` działa; alert log pusty (brak prób połączeń).

---

### Faza 3 — Hooks (routing enforcement)

| Krok | Opis | Plik | Status |
|---|---|---|---|
| 3.1 | Hooks config (PreToolUse, PostToolUse, SessionStart) | `.github/hooks/context-mode.json` | 🔲 |
| 3.2 | Weryfikacja hooków: restart VS Code, nowa sesja Copilot | VS Code | 🔲 |
| 3.3 | Test session continuity: wymuś compaction, sprawdź resume | Copilot Chat | 🔲 |
| 3.4 | Pomiar: `ctx stats` po sesji roboczej — sprawdź % redukcji | Copilot Chat | 🔲 |

**Kryterium akceptacji Fazy 3**: Hooks aktywne; `ctx stats` pokazuje savings > 0; session restore po compaction działa.

---

### Faza 4 — copilot-instructions.md merge

| Krok | Opis | Plik | Status |
|---|---|---|---|
| 4.1 | Append sekcji 13 (routing context-mode) | `.github/copilot-instructions.md` | 🔲 |
| 4.2 | Weryfikacja: istniejące agenty działają (ADR query, BC routing) | Copilot Chat | 🔲 |
| 4.3 | Wywołanie `@copilot-setup-maintainer` (Workflow 11 + 7) | Copilot Chat | 🔲 |

**Kryterium akceptacji Fazy 4**: Agenty projektowe (ADR, BC) działają jak wcześniej; context-mode routing aktywny równolegle.

---

### Faza 5 — Monitoring web UI (wiśnia na torcie)

| Krok | Opis | Plik | Status |
|---|---|---|---|
| 5.1 | Dozzle serwis w docker-compose (profil `monitoring`) | `docker-compose.yaml` | 🔲 |
| 5.2 | VS Code task: `Context-Mode: Start + Dozzle` | `.vscode/tasks.json` | 🔲 |
| 5.3 | Weryfikacja: `http://localhost:9999` — widoczne logi context-mode | przeglądarka | 🔲 |
| 5.4 | Test alertu: wymuszenie próby połączenia; weryfikacja w Dozzle + alert log | terminal | 🔲 |

**Kryterium akceptacji Fazy 5**: Dozzle dostępny; filtr na `ecommerceapp-context-mode`; `[SUSPICIOUS]` widoczne w real-time.

---

## Weryfikacja end-to-end

Po ukończeniu wszystkich faz:

```
1. docker compose up -d context-mode
2. VS Code: Copilot Chat → "ctx stats"              → odpowiedź z 0 savings
3. Copilot Chat → "ctx_execute javascript console.log(47*6)"  → "282"
4. Copilot Chat → analiza src/ przez ctx_execute_file         → wynik bez raw content
5. docker logs ecommerceapp-context-mode | grep SUSPICIOUS    → brak wyników
6. cat .ctx-network-alerts.log                                → pusty lub nie istnieje
```

---

## Zależności i kolejność

```
RAG MCP server stabilny     ← wymagane PRZED startem Fazy 1
        ↓
Faza 1 (Docker)
        ↓
Faza 2 (MCP connection)
        ↓
Faza 3 (Hooks)              ← można pominąć i wrócić; MCP działa bez hooków (~60% compliance)
        ↓
Faza 4 (instructions merge) ← wymaga @copilot-setup-maintainer po ukończeniu
        ↓
Faza 5 (Dozzle)             ← opcjonalne; nie blokuje pozostałych faz
```

---

## Nowe pliki i modyfikacje — rejestr

| Plik | Akcja | Faza | Wpływ na istniejący setup |
|---|---|---|---|
| `docker/context-mode/network-monitor.js` | Nowy | 1 | Brak |
| `docker/context-mode/entrypoint.sh` | Nowy | 1 | Brak |
| `Dockerfile-context-mode` | Nowy | 1 | Brak |
| `docker-compose.yaml` | Delta (2 serwisy + 1 volume) | 1/5 | Nie rusza istniejących serwisów |
| `.vscode/mcp.json` | Nowy | 2 | Dodaje MCP server; RAG pozostaje |
| `.vscode/tasks.json` | Delta (4 nowe taski) | 2/5 | Istniejące taski bez zmian |
| `.gitignore` | Delta (1 linia) | 2 | Brak |
| `.github/hooks/context-mode.json` | Nowy | 3 | Wymaga @copilot-setup-maintainer |
| `.github/copilot-instructions.md` | Append (sekcja 13) | 4 | Wymaga @copilot-setup-maintainer |

---

## Multi-runtime uwagi

| Runtime | Wymagane zmiany | |
|---|---|---|
| Docker Desktop (Windows) | Brak — gotowe | ✅ |
| Rancher Desktop (containerd) | `docker` → `nerdctl` w hooks + tasks; weryfikacja `--network none` | ⚠ Dokumentuj lokalnie |
| Rancher Desktop (dockerd) | Brak — identyczne z Docker Desktop | ✅ |
| Podman (rootless) | `docker` → `podman` w hooks + tasks + mcp.json; `docker-compose` → `podman-compose` | ⚠ Dokumentuj lokalnie |

> **Zasada**: pliki w repozytorium używają `docker`. Podmiana na `podman`/`nerdctl` jest zmianą lokalną (nie commitowaną) lub przez env var w tasków VS Code.

---

## Upgrade policy

1. Sprawdź release notes na [github.com/mksglu/context-mode/releases](https://github.com/mksglu/context-mode/releases)
2. Zmień tag w `Dockerfile-context-mode` (jedna linia)
3. `docker compose build context-mode`
4. `docker compose up -d context-mode`
5. `ctx doctor` w Copilot Chat — weryfikacja
6. Commit: `chore: bump context-mode to vX.Y.Z`
