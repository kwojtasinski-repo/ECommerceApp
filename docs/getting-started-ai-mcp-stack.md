# AI MCP Stack Setup Guide (Human-Friendly)

This guide is the shortest practical path to run both:

1. context-mode (analysis sandbox), and
2. RAG (docs/ADR retrieval)

in VS Code Copilot Chat.

It covers:
- ingest
- STDIO mode
- HTTP mode
- local/source and container variants

## 0. Prerequisites

- Docker Desktop running
- VS Code + GitHub Copilot Chat
- Repo cloned locally

From repo root:

```powershell
cd C:\Projekty\ECommerceApp
```

## 1. Context-mode setup (step-by-step)

### 1.1 Bootstrap once

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/context-mode-bootstrap.ps1
```

### 1.2 Recreate core containers (safe refresh)

```powershell
docker compose up -d --force-recreate context-mode adguard qdrant
```

### 1.3 Enable MCP server in VS Code

- Open Copilot Chat
- Open MCP panel
- Enable: `ecommerceapp-context-mode`

### 1.4 Quick smoke test in chat

Ask Copilot:

- `Run ctx_doctor and summarize result in one sentence.`
- `Run ctx_execute javascript console.log(6*7)`

Expected second result: `42`.

### 1.5 Where to see logs

Container stream:

```powershell
docker logs -f ecommerceapp-context-mode
```

Direct files in container:

```powershell
docker exec -it ecommerceapp-context-mode sh -lc "tail -f /home/ctxmode/.context-mode/runtime.log"
docker exec -it ecommerceapp-context-mode sh -lc "tail -f /home/ctxmode/.context-mode/hooks.log"
docker exec -it ecommerceapp-context-mode sh -lc "tail -f /tmp/.ctx-network-alerts.log"
docker exec -it ecommerceapp-context-mode sh -lc "tail -f /home/node/.vscode/context-mode/posttooluse-debug.log"
docker exec -it ecommerceapp-context-mode sh -lc "tail -f /home/node/.vscode/context-mode/precompact-debug.log"
docker exec -it ecommerceapp-context-mode sh -lc "tail -f /home/node/.vscode/context-mode/sessionstart-debug.log"
```

## 2. RAG setup (step-by-step)

## 2.1 Ingest docs (required)

Python ingest in container:

```powershell
docker compose build rag-tools
docker compose --profile rag up -d qdrant
docker compose --profile rag run --rm rag-tools python ingest.py
```

.NET ingest in container:

```powershell
docker compose build rag-dotnet
docker compose --profile rag-dotnet up -d qdrant
docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll
```

Run ingest again after docs/context changes.

## 2.2 RAG matrix: local vs container, STDIO vs HTTP

### A) Python, local source, STDIO

- MCP server: `ecommerceapp-rag-python`
- Uses local `tools/rag/.venv/Scripts/python.exe`
- Best for local dev speed

Enable in MCP panel, then ask:

- `List ADRs`
- `What does ADR-0029 say about hooks?`

### B) Python, container, STDIO

- MCP server: `ecommerceapp-rag-python-docker`
- Uses `docker run` per session
- Good when you want runtime parity with container image

### C) Python, HTTP

Start server:

```powershell
docker compose --profile rag-python-http up -d rag-python-http
```

Enable MCP server: `ecommerceapp-rag-python-http`.

### D) .NET, local source, STDIO

- MCP server: `ecommerceapp-rag-dotnet`
- Uses `dotnet run` from `tools/rag-dotnet/src/RagTools.Mcp`

### E) .NET, container, STDIO

- MCP server: `ecommerceapp-rag-dotnet-docker`
- Uses container image

### F) .NET, HTTP

Start server:

```powershell
docker compose --profile rag-dotnet-http up -d rag-dotnet-http
```

Enable MCP server: `ecommerceapp-rag-dotnet-http`.

## 2.3 Recommended default for teams

- context-mode: enabled
- RAG: Python local STDIO (`ecommerceapp-rag-python`) for day-to-day work
- HTTP variants: use for integration/testing and persistent service scenarios

## 3. Day-to-day command cheatsheet

Start everything useful for mixed work:

```powershell
docker compose up -d context-mode adguard qdrant rag-python-http rag-dotnet-http
```

Stop HTTP servers only:

```powershell
docker compose --profile rag-dotnet-http --profile rag-python-http down
```

Full stop (context-mode + monitoring):

```powershell
docker compose --profile context-mode --profile monitoring down
```

## 4. Token-control routing rule (important)

Use this default behavior:

- analysis/summarization/computation -> context-mode first
- docs/ADR/project knowledge -> RAG first
- direct file reads only when user explicitly points to a specific file or when fallback is mandatory

This keeps context usage lower and reduces random token spikes.

## 5. Troubleshooting fast answers

- `ctx_stats` still shows `0 calls` after real ctx calls:
  - likely telemetry/version issue, not necessarily no usage
  - update context-mode and restart session

- RAG returns stale answers:
  - run ingest again

- MCP server not visible in chat tools:
  - check MCP panel toggle
  - verify container is running (`docker ps`)

- context-mode logs look empty:
  - use `docker logs -f ecommerceapp-context-mode`
  - verify runtime/hooks log files from section 1.5
