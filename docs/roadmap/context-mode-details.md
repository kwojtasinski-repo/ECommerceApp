# context-mode — szczegóły implementacji

> Companion document do [`context-mode-integration.md`](./context-mode-integration.md)
> Zawiera pełne konfiguracje wszystkich plików. Kopiuj bez modyfikacji, chyba że zaznaczono inaczej.

---

## Wstęp

### Dlaczego context-mode

GitHub Copilot od nowego miesiąca wprowadza limity requestów. Każde wywołanie
narzędzia (run_in_terminal, read_file, grep_search, fetch URL) wrzuca surowe dane
do okna kontekstu — co bezpośrednio zwiększa liczbę requestów do modelu.

Przykłady raw output bez integracji:

- `dotnet test` (30 suite) → 6 KB w kontekście zamiast 337 B po
- Playwright snapshot → 56 KB zamiast 299 B
- Analiza 47 plików → 700 KB zamiast 3.6 KB
- Cała sesja robocza → 315 KB zamiast 5.4 KB (98% redukcja)

### Cel

Zmniejszenie zużycia kontekstu bez zmiany sposobu pracy. Copilot nadal wydaje
polecenia — context-mode sandbox je wykonuje i oddaje tylko wynik (stdout),
nie raw data.

### Co uzyskujemy

- `ctx_execute("shell", "dotnet test")` → 337 B zamiast 6 KB
- `ctx_batch_execute(["dotnet build", "dotnet test"])` → jeden call zamiast dwóch
- Session continuity: po compaction model wraca do ostatniego zadania bez pytań
- RAG MCP działa jak dotychczas (docs projektu, ADR, architektura)
- Monitoring: `.ctx-network-alerts.log` + Dozzle web UI

### Czego się obawiamy i jak temu zapobiegamy

**Exfiltracja danych przez MCP server**
context-mode MCP widzi argumenty toolów (kod przekazany do ctx_execute,
ścieżki plików, zawartość ctx_execute_file). Przy dostępie sieciowym mógłby
te dane wysłać poza maszynę.

Mitigacja: `--network none` — kontener fizycznie nie może nawiązać żadnego
połączenia sieciowego. Node.js monitoring hook loguje każdą próbę do
`.ctx-network-alerts.log`.

**Niekontrolowane upgrade'y**
`npm install -g context-mode` pobiera zawsze najnowszą wersję. Breaking changes
zdarzają się (172 releases).

Mitigacja: git clone pinowany tag w Dockerfile. Cały team na tej samej wersji.
Upgrade = jeden commit + review.

**Konflikt z istniejącą konfiguracją Copilota**
Projekt ma rozbudowane `.github/copilot-instructions.md`, agenty, ADR routing.
Nadpisanie pliku zniszczyłoby całą konfigurację.

Mitigacja: wyłącznie append (sekcja 13 na końcu). Istniejące sekcje 1-12 bez zmian.

**Różnice między runtime'ami (Docker Desktop, Rancher, Podman)**
`internal: true` Docker network ma różne edge case'y między runtime'ami.

Mitigacja: `--network none` działa identycznie na wszystkich runtime'ach.

### Ryzyka i konsekwencje

| Ryzyko | P-stwo | Wpływ | Mitigacja |
|---|---|---|---|
| context-mode próbuje połączenia z zewnętrzem | Możliwe | Wysoki | `--network none` blokuje; hook + alert log informuje |
| Breaking change po upgrade | Niskie (pinowana wersja) | Średni | Świadomy upgrade przez cały team |
| Hooks interferują z RAG MCP | Niskie | Niski | `CONTEXT_MODE_EXTERNAL_MCP_NUDGE_EVERY=50` |
| copilot-instructions.md kolizja | Bardzo niskie | Wysoki | Append only; sekcja 13 ma niższy priorytet |
| Podman rootless różnice | Możliwe | Niski | Podmiana docker→podman lokalnie (nie commitowana) |

---

## Faza 1 — Docker sandbox

### `docker/context-mode/network-monitor.js`

```js
/**
 * Network monitoring hook dla context-mode.
 * Nadpisuje net.Socket.connect PRZED uruchomieniem serwera MCP.
 * Każda próba połączenia sieciowego trafia do stderr (docker logs) i
 * do pliku alertów jeśli target jest poza siecią prywatną.
 * Działa bez kernel capabilities — czysty Node.js, cross-runtime.
 */
'use strict';

const net = require('net');
const fs  = require('fs');

const ALERT_LOG = '/workspace/.ctx-network-alerts.log';

// Zakresy IP uznawane za wewnętrzne (bezpieczne)
const INTERNAL = [
  /^127\./,
  /^::1$/,
  /^localhost$/i,
  /^0\.0\.0\.0$/,
  /^10\./,
  /^172\.(1[6-9]|2\d|3[01])\./,
  /^192\.168\./,
  /^fd[0-9a-f]{2}:/i,   // IPv6 ULA fc00::/7
];

function classify(host) {
  return INTERNAL.some(r => r.test(String(host))) ? 'INFO' : 'SUSPICIOUS';
}

const _connect = net.Socket.prototype.connect;

net.Socket.prototype.connect = function (options, ...rest) {
  const host = typeof options === 'object'
    ? (options.host || options.hostname || 'unknown')
    : String(options);
  const port = typeof options === 'object' ? options.port : rest[0];
  const level = classify(host);
  const ts    = new Date().toISOString();
  const line  = `[NET-MONITOR] [${level}] ${ts} → ${host}:${port}`;

  process.stderr.write(line + '\n');

  if (level === 'SUSPICIOUS') {
    try { fs.appendFileSync(ALERT_LOG, line + '\n'); } catch (_) { /* workspace unmounted */ }
  }

  return _connect.call(this, options, ...rest);
};
```

### `docker/context-mode/entrypoint.sh`

```bash
#!/bin/sh
# Wrapper uruchamiający context-mode z załadowanym monitoring hookiem.
# node --require ładuje network-monitor.js PRZED jakimkolwiek kodem MCP servera.
exec node \
  --require /app/network-monitor.js \
  /app/start.mjs \
  "$@"
```

### `Dockerfile-context-mode`

```dockerfile
# ─────────────────────────────────────────────────────────────────
# Stage 1: Build context-mode ze źródła (pinowany tag, audytowalny)
# ─────────────────────────────────────────────────────────────────
FROM node:22-alpine AS builder

RUN apk add --no-cache git

# Klonuj dokładnie ten tag — zmień tutaj przy upgrade, nigdzie indziej
ARG CONTEXT_MODE_TAG=v1.0.146
RUN git clone --depth 1 --branch ${CONTEXT_MODE_TAG} \
    https://github.com/mksglu/context-mode.git /build

WORKDIR /build
RUN npm ci --production --ignore-scripts

# ─────────────────────────────────────────────────────────────────
# Stage 2: Minimalny runtime image
# ─────────────────────────────────────────────────────────────────
FROM node:22-alpine

# Non-root user — security hardening
RUN addgroup -S ctxmode && adduser -S ctxmode -G ctxmode

# Kopiuj zbudowaną aplikację
COPY --from=builder /build /app
COPY docker/context-mode/network-monitor.js /app/network-monitor.js
COPY docker/context-mode/entrypoint.sh     /entrypoint.sh

RUN chmod +x /entrypoint.sh \
 && mkdir -p /root/.context-mode \
 && chown -R ctxmode:ctxmode /app /root/.context-mode

USER ctxmode
WORKDIR /workspace

ENTRYPOINT ["/entrypoint.sh"]
```

> **Upgrade**: zmień `CONTEXT_MODE_TAG` w Dockerfile → `docker compose build context-mode` → `docker compose up -d context-mode`.

---

## Faza 1+5 — docker-compose.yaml (delta)

Dodaj **na końcu** sekcji `services:` i uzupełnij sekcję `volumes:`:

```yaml
  # ── Context-Mode MCP sandbox ──────────────────────────────────────────────────
  context-mode:
    build:
      context: .
      dockerfile: Dockerfile-context-mode
      args:
        CONTEXT_MODE_TAG: "v1.0.146"
    image: ecommerceapp/context-mode:1.0.146
    container_name: ecommerceapp-context-mode
    stdin_open: true
    tty: false
    restart: unless-stopped
    volumes:
      - .:/workspace                           # dostęp do plików projektu (R/W)
      - context-mode-data:/root/.context-mode  # SQLite session DB — persystuje między sesjami
    environment:
      CTX_FETCH_STRICT: "1"                          # blokuje loopback + RFC1918 (defense-in-depth)
      CONTEXT_MODE_EXTERNAL_MCP_NUDGE_EVERY: "50"    # zmniejsza szum dla RAG MCP calls
    network_mode: "none"    # zero egress, zero ingress — cross-runtime (Docker Desktop/Rancher/Podman)

  # ── Dozzle — web log viewer (profil: monitoring) ──────────────────────────────
  # Uruchamiaj tylko gdy potrzebujesz: docker compose --profile monitoring up -d dozzle
  # Dostęp: http://localhost:9999 (tylko localhost)
  dozzle:
    image: amir20/dozzle:latest
    container_name: ecommerceapp-dozzle
    profiles: [monitoring]
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro  # read-only — nie daje pełnego dostępu do Docker daemon
    ports:
      - "127.0.0.1:9999:8080"          # eksponuj tylko na localhost
    environment:
      DOZZLE_FILTER: "name=ecommerceapp-context-mode"  # tylko nasz kontener
    restart: unless-stopped
```

W sekcji `volumes:` (na samym końcu pliku) dodaj:

```yaml
  context-mode-data:    # SQLite session DB dla context-mode
```

---

## Faza 2 — `.vscode/mcp.json`

> Jeśli plik już istnieje (konfiguracja RAG), dodaj tylko klucz `context-mode` do istniejącego obiektu `servers`.

```json
{
  "servers": {
    "context-mode": {
      "command": "docker",
      "args": [
        "exec",
        "-i",
        "ecommerceapp-context-mode",
        "node",
        "--require", "/app/network-monitor.js",
        "/app/start.mjs"
      ]
    }
  }
}
```

> **Podman**: podmień `"docker"` → `"podman"` lokalnie. Nie commituj tej zmiany.
> **Rancher Desktop (containerd)**: podmień `"docker"` → `"nerdctl"` lokalnie.

---

## Faza 2 — `.vscode/tasks.json` (delta)

Dodaj do tablicy `tasks`:

```json
{
  "label": "Context-Mode: Start",
  "type": "shell",
  "command": "docker compose up -d context-mode",
  "detail": "Uruchom context-mode sandbox MCP (network: none, monitoring: on).",
  "group": "build",
  "presentation": { "reveal": "silent", "panel": "shared" }
},
{
  "label": "Context-Mode: Stop",
  "type": "shell",
  "command": "docker compose stop context-mode",
  "detail": "Zatrzymaj context-mode.",
  "presentation": { "reveal": "silent", "panel": "shared" }
},
{
  "label": "Context-Mode: Network Alerts",
  "type": "shell",
  "command": "if (Test-Path .ctx-network-alerts.log) { Get-Content .ctx-network-alerts.log -Wait -Tail 50 } else { Write-Host 'Brak alertów — plik nie istnieje (dobry znak).' }",
  "detail": "Tail alertów sieciowych z context-mode. Pusty = brak podejrzanych połączeń.",
  "presentation": { "reveal": "always", "panel": "dedicated" }
},
{
  "label": "Context-Mode: Start + Dozzle",
  "type": "shell",
  "command": "docker compose --profile monitoring up -d context-mode dozzle ; Start-Sleep 2 ; Start-Process 'http://localhost:9999'",
  "detail": "Uruchom context-mode + Dozzle log viewer. Otwiera http://localhost:9999.",
  "presentation": { "reveal": "silent", "panel": "shared" }
}
```

---

## Faza 2 — `.gitignore` (delta)

Dodaj jedną linię (np. na końcu sekcji z plikami tymczasowymi):

```gitignore
# context-mode network alerts log (auto-generated, nie commituj)
.ctx-network-alerts.log
```

---

## Faza 3 — `.github/hooks/context-mode.json`

> Nowy plik. Katalog `.github/hooks/` utwórz jeśli nie istnieje.
> **Po dodaniu tego pliku wywołaj `@copilot-setup-maintainer` (Workflow 11 + 7).**

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "type": "command",
        "command": "docker exec -i ecommerceapp-context-mode context-mode hook vscode-copilot pretooluse"
      }
    ],
    "PostToolUse": [
      {
        "type": "command",
        "command": "docker exec -i ecommerceapp-context-mode context-mode hook vscode-copilot posttooluse"
      }
    ],
    "SessionStart": [
      {
        "type": "command",
        "command": "docker exec -i ecommerceapp-context-mode context-mode hook vscode-copilot sessionstart"
      }
    ]
  }
}
```

> **Podman**: podmień `docker exec` → `podman exec` lokalnie.

---

## Faza 4 — `.github/copilot-instructions.md` (append)

> **Dopisz na samym końcu** istniejącego pliku. NIE modyfikuj sekcji 1-12.
> Sekcja 13 ma niższy priorytet niż reguły projektowe (ADR, BC, agenty).
> **Po modyfikacji wywołaj `@copilot-setup-maintainer` (Workflow 11 + 7).**

```markdown
## 13. Context sandbox (context-mode)

context-mode MCP tools są dostępne. Sandboxują surowe dane — chronią okno
kontekstu. **Jeden niezaroutowany call może wrzucić 56 KB do kontekstu.**

### Myśl w kodzie (MANDATORY dla analizy danych)

Analiza, liczenie, filtrowanie, porównywanie, parsowanie → **napisz skrypt**
przez `ctx_execute(language, code)`, `console.log()` tylko wynik.
NIE czytaj surowych danych do kontekstu. Jeden skrypt zastępuje 10 tool calls.

```js
// Przed: 47 × read_file = 700 KB.  Po: 1 × ctx_execute = 3.6 KB.
ctx_execute("javascript", `
  const files = require('fs').readdirSync('src').filter(f => f.endsWith('.cs'));
  files.forEach(f => console.log(f + ': ' + require('fs').readFileSync('src/'+f,'utf8').split('\\n').length + ' linii'));
`);
```

### Priorytety narzędzi (gdy nie określa tego reguła projektowa)

0. **MEMORY**: `ctx_search(sort: "timeline")` — po resume sprawdź historię przed pytaniem użytkownika.
1. **GATHER**: `ctx_batch_execute(commands, queries)` — wiele komend + search w JEDNYM callu.
2. **FOLLOW-UP**: `ctx_search(queries: ["q1", "q2"])` — wiele pytań naraz, jeden call.
3. **PROCESSING**: `ctx_execute(language, code)` lub `ctx_execute_file(path, language, code)` — sandbox.
4. **WEB**: `ctx_fetch_and_index(url, source)` → `ctx_search(queries)` — surowy HTML nigdy do kontekstu.
5. **INDEX**: `ctx_index(content, source)` — przechowaj w FTS5 do późniejszego wyszukiwania.

### Przekierowania (REDIRECTED)

| Zamiast | Użyj |
|---|---|
| `run_in_terminal` (output > 20 linii) | `ctx_batch_execute` lub `ctx_execute("shell", ...)` |
| `read_file` do **analizy** | `ctx_execute_file(path, language, code)` |
| `grep_search` na dużych wynikach | `ctx_execute("shell", "grep ...")` w sandboxie |
| `fetch` / WebFetch | `ctx_fetch_and_index(url)` → `ctx_search` |

> `read_file` jest prawidłowy gdy edytujesz plik. Sandbox tylko gdy **analizujesz**.

### Uwaga: dwa systemy session memory

| System | Narzędzie | Cel |
|---|---|---|
| context-mode session DB | `ctx_search(source: "compaction")` | Historia toolów, pliki edytowane, decyzje w tej sesji |
| VS Code session store | `session_store_sql` | Historia sesji VS Code, poprzednie konwersacje |

Te systemy są **niezależne** — nie mieszaj ich użycia.

### ctx commands

| Komenda | Akcja |
|---|---|
| `ctx stats` | Wywołaj `ctx_stats`; pokaż pełny output |
| `ctx doctor` | Wywołaj `ctx_doctor`; uruchom zwrócone komendy shell |
| `ctx upgrade` | Wywołaj `ctx_upgrade`; uruchom zwrócone komendy shell |
| `ctx purge` | Wywołaj `ctx_purge` z `confirm: true`. Ostrzega przed wyczyszczeniem KB |
```

---

## Faza 5 — Dozzle (monitoring web UI)

Dozzle to **aplikacja webowa** — zero konfiguracji po stronie użytkownika.

| Akcja | Komenda / URL |
|---|---|
| Uruchom | `docker compose --profile monitoring up -d dozzle` |
| Lub przez task | VS Code: `Context-Mode: Start + Dozzle` |
| Dostęp | http://localhost:9999 (tylko localhost) |
| Logi real-time | Kliknij kontener `ecommerceapp-context-mode` |
| Szukaj alertów | Wpisz `SUSPICIOUS` w polu wyszukiwania |
| Zatrzymaj | `docker compose --profile monitoring stop dozzle` |

Dozzle nie ma bazy danych, nie loguje poza sesją, nie eksponuje danych na zewnątrz.

---

## Weryfikacja po każdej fazie

### Faza 1

```powershell
docker compose up -d context-mode
docker ps --filter name=ecommerceapp-context-mode
docker logs ecommerceapp-context-mode
```

Oczekiwane: kontener `Up`, brak błędów w logach.

### Faza 2

```
# W Copilot Chat:
ctx stats
```

Oczekiwane: odpowiedź z context-mode (0 savings na początku — normalne).

```
# W Copilot Chat:
ctx_execute javascript console.log(6*7)
```

Oczekiwane: `42`.

```powershell
# Sprawdź brak alertów:
Test-Path .ctx-network-alerts.log
```

Oczekiwane: `False` lub pusty plik.

### Faza 3

```
# W Copilot Chat (po restarcie VS Code):
ctx stats
```

Oczekiwane: `ctx_stats` wywoływany przez SessionStart hook — widoczne w logach.

### Faza 4

```
# W Copilot Chat — test że agenty projektowe działają:
"Pokaż ADR-0013"
```

Oczekiwane: RAG MCP zwraca treść ADR; context-mode routing nie interferuje.

### Faza 5

Otwórz http://localhost:9999 → powinna być widoczna lista kontenerów z `ecommerceapp-context-mode`.

---

## Upgrade context-mode — procedura

1. Sprawdź [release notes](https://github.com/mksglu/context-mode/releases) — szukaj breaking changes
2. Zmień tag w `Dockerfile-context-mode` (linia `ARG CONTEXT_MODE_TAG=...`)
3. Rebuild: `docker compose build context-mode`
4. Restart: `docker compose up -d context-mode`
5. Weryfikacja: `ctx doctor` w Copilot Chat
6. Commit: `chore: bump context-mode to vX.Y.Z`

---

## Multi-runtime — podmiana lokalna

> Nie commituj tych zmian. Każdy deweloper konfiguruje lokalnie.

**Podman:**
- `.vscode/mcp.json`: `"docker"` → `"podman"`
- `.github/hooks/context-mode.json`: `docker exec` → `podman exec`
- VS Code tasks: `docker compose` → `podman-compose`

**Rancher Desktop (containerd/nerdctl):**
- `.vscode/mcp.json`: `"docker"` → `"nerdctl"`
- `.github/hooks/context-mode.json`: `docker exec` → `nerdctl exec`
- VS Code tasks: `docker compose` → `nerdctl compose`

**Rancher Desktop (dockerd):** Brak zmian — identyczne z Docker Desktop.
