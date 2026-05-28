---
name: setup-mcp-clients
description: >
  Configure MCP clients for a NEW project. Covers VS Code (`.vscode/mcp.json`), GitHub
  Copilot Web (`.github/copilot/mcp.json`), and Visual Studio 17.14+ (slightly different
  JSON shape). Side-by-side schema comparison for the same RAG + context-mode endpoints.
argument-hint: "<client> <project-name>"
---

# setup-mcp-clients — wire up MCP clients

MCP servers (RAG, context-mode) are reachable from three different clients in this
project's ecosystem. Each client reads a DIFFERENT config file with a slightly
DIFFERENT JSON shape. This skill documents all three.

---

## When to use

- Brand-new project, no `.vscode/mcp.json` / `.github/copilot/mcp.json` yet.
- Adding a NEW server (e.g. a second project's RAG) to an existing client.
- Switching a client between stdio and HTTP transport.

## When NOT to use

- Bringing up the servers themselves — use E1 / E2.
- Diagnosing why an MCP tool returns errors — use
  [`.github/skills/diagnose-rag/SKILL.md`](../diagnose-rag/SKILL.md) (RAG) or
  [`.github/skills/ctx-doctor-playbook/SKILL.md`](../ctx-doctor-playbook/SKILL.md)
  (context-mode).

---

## Three clients, three configs

| Client | Config path | Schema root key | Stdio supported? | HTTP supported? |
|---|---|---|---|---|
| VS Code (Copilot Chat) | `.vscode/mcp.json` | `servers` | ✅ | ✅ |
| GitHub Copilot Web | `.github/copilot/mcp.json` | `mcpServers` | ❌ | ✅ |
| Visual Studio 17.14+ | `%USERPROFILE%/.mcp/servers.json` OR per-solution `.mcp/servers.json` | `mcpServers` | ✅ (Windows only) | ✅ |

---

## Steps

### 1. Confirm which clients the project needs

| Project usage | Required clients |
|---|---|
| VS Code only | `.vscode/mcp.json` |
| Solo dev with both VS Code + Visual Studio | `.vscode/mcp.json` + VS user-scope file |
| Team with GitHub.com Copilot | + `.github/copilot/mcp.json` (HTTP, publicly reachable) |
| Public GitHub repo with PR reviewers using Copilot Web | always include `.github/copilot/mcp.json` |

### 2. VS Code — `.vscode/mcp.json`

The richest client. Supports stdio (preferred for local Docker containers) and HTTP.

```jsonc
{
  "servers": {
    "<project>-rag-dotnet": {
      "type": "http",
      "url": "http://localhost:3001"
    },
    "<project>-rag-python": {
      "type": "http",
      "url": "http://localhost:3002"
    },
    "<project>-context-mode": {
      "type": "stdio",
      "command": "docker",
      "args": [
        "exec", "-i",
        "<project>-context-mode",
        "node", "/app/cli.bundle.mjs", "serve"
      ]
    }
  }
}
```

Reload via Command Palette → "MCP: Reload servers".

### 3. GitHub Copilot Web — `.github/copilot/mcp.json`

Stdio is NOT supported (Copilot Web runs in GitHub's infrastructure, has no `docker`
binary). HTTP only. The endpoint MUST be reachable from GitHub's network — usually
means deploying behind a public reverse proxy with HTTPS + auth.

```jsonc
{
  "mcpServers": {
    "<project>-rag": {
      "type": "http",
      "url": "https://<your-public-host>/rag",
      "headers": {
        "Authorization": "Bearer ${env:MCP_RAG_TOKEN}"
      }
    },
    "<project>-context-mode": {
      "type": "http",
      "url": "https://<your-public-host>/context-mode",
      "headers": {
        "Authorization": "Bearer ${env:MCP_CTX_TOKEN}"
      }
    }
  }
}
```

Token storage: GitHub repo secrets, surfaced as `env:` references. NEVER inline.

If the project is single-developer and the user doesn't use Copilot Web, omit this
file entirely.

### 4. Visual Studio 17.14+ — `.mcp/servers.json`

Visual Studio reads `%USERPROFILE%\.mcp\servers.json` (user-scope) and
`<solution-dir>\.mcp\servers.json` (solution-scope; takes precedence). Stdio is
Windows-only but works the same as VS Code.

```jsonc
{
  "mcpServers": {
    "<project>-rag-dotnet": {
      "type": "http",
      "url": "http://localhost:3001"
    },
    "<project>-context-mode": {
      "type": "stdio",
      "command": "docker.exe",
      "args": [
        "exec", "-i",
        "<project>-context-mode",
        "node", "/app/cli.bundle.mjs", "serve"
      ]
    }
  }
}
```

Note: Visual Studio uses `docker.exe` (not bare `docker`) to bypass `PATH` ordering
issues with WSL.

### 5. HTTP transport — adding session header support

For HTTP servers that require `mcp-session-id` (newer MCP spec), VS Code handles it
automatically. For custom clients (e.g. Postman tests), add:

```http
POST /mcp HTTP/1.1
Content-Type: application/json
Mcp-Session-Id: <uuid>
```

The first request without a session ID gets one back in the response; reuse it for
subsequent requests in the same conversation.

### 6. Smoke test each client

VS Code: open chat → type "what MCP servers are connected?" → expected: list of 3.

Copilot Web: open repository in github.com → click the Copilot chat icon → expected:
servers from `.github/copilot/mcp.json` listed.

Visual Studio: View → Other Windows → MCP Servers → expected: list of registered
servers with green status.

---

## Common mistakes

- **Mixing stdio + HTTP for the SAME server across clients.** A stdio server reads
  from stdin; if a second client tries to attach, both fail with cryptic
  serialization errors. Pick one transport per server — usually HTTP (multi-client
  safe), with stdio reserved for fully-isolated dev boxes.
- **Hard-coding `localhost:3001` in `.github/copilot/mcp.json`.** Copilot Web runs
  remotely; `localhost` resolves to GitHub's own infrastructure, not your laptop.
  Always use a publicly reachable hostname.
- **Forgetting `mcp-session-id` header in custom HTTP clients.** Modern MCP servers
  require it. Symptom: every request gets a fresh session, so multi-turn tool calls
  lose context. VS Code and Copilot handle it automatically; custom Postman/curl
  scripts must add it manually.
- **Putting tokens inline in `.github/copilot/mcp.json`.** That file is committed to
  the repo — inline tokens leak. Use `${env:NAME}` references and store tokens as
  GitHub repo secrets.
- **Using bare `docker` on Visual Studio Windows.** `PATH` ordering between
  `docker.exe` (Docker Desktop) and WSL `docker` shim is fragile. Use the explicit
  `.exe` to avoid 5-minute debugging sessions when VS picks the wrong one.
- **Different container names across `.vscode/mcp.json` and the compose file.** The
  stdio command does `docker exec -i <name>` — if `container_name:` in
  `docker-compose.yaml` says `acmeapp-context-mode` but the MCP config says
  `acmeapp_context_mode`, the exec fails silently and VS Code shows "server crashed".

---

## Worked example: AcmeApp with all 3 clients

Project layout:

```text
acmeapp/
├── .vscode/mcp.json                # dev box
├── .github/copilot/mcp.json        # public Copilot Web
├── docker-compose.yaml             # services
└── (Visual Studio user file in %USERPROFILE%\.mcp\servers.json — not committed)
```

`.vscode/mcp.json` — stdio for context-mode, HTTP for RAG (local):

```jsonc
{
  "servers": {
    "acmeapp-rag-dotnet":    { "type": "http", "url": "http://localhost:3001" },
    "acmeapp-rag-python":    { "type": "http", "url": "http://localhost:3002" },
    "acmeapp-context-mode":  {
      "type": "stdio",
      "command": "docker",
      "args": ["exec", "-i", "acmeapp-context-mode", "node", "/app/cli.bundle.mjs", "serve"]
    }
  }
}
```

`.github/copilot/mcp.json` — HTTP only, behind public reverse proxy:

```jsonc
{
  "mcpServers": {
    "acmeapp-rag":          { "type": "http", "url": "https://mcp.acmeapp.example/rag",          "headers": { "Authorization": "Bearer ${env:MCP_RAG_TOKEN}" } },
    "acmeapp-context-mode": { "type": "http", "url": "https://mcp.acmeapp.example/context-mode", "headers": { "Authorization": "Bearer ${env:MCP_CTX_TOKEN}" } }
  }
}
```

`%USERPROFILE%\.mcp\servers.json` (user-only; not committed):

```jsonc
{
  "mcpServers": {
    "acmeapp-rag-dotnet":    { "type": "http", "url": "http://localhost:3001" },
    "acmeapp-context-mode":  {
      "type": "stdio",
      "command": "docker.exe",
      "args": ["exec", "-i", "acmeapp-context-mode", "node", "/app/cli.bundle.mjs", "serve"]
    }
  }
}
```

---

## Related skills / docs

- [.github/skills/setup-rag-new-project/SKILL.md](../setup-rag-new-project/SKILL.md) (E1)
- [.github/skills/setup-context-mode-new-project/SKILL.md](../setup-context-mode-new-project/SKILL.md) (E2)
- [.github/skills/setup-auto-cache-hook/SKILL.md](../setup-auto-cache-hook/SKILL.md) (E5)
- [.github/instructions/mcp-routing.instructions.md](../../instructions/mcp-routing.instructions.md) — routing rules across all clients
- [.vscode/mcp.json](../../../.vscode/mcp.json) — this repo's reference config
- [.github/copilot/mcp.json](../../copilot/mcp.json) — this repo's reference config
- [docs/adr/0028/0028-remote-multitenant-rag-ingest.md](../../../docs/adr/0028/0028-remote-multitenant-rag-ingest.md) — collection naming
