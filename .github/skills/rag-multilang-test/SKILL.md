---
name: rag-multilang-test
description: >
  Verify that a new entry added to multilingual-glossary.yaml correctly expands
  Polish / German queries to the English term used in the corpus. Confirms both
  Python (:3002) and .NET (:3001) HTTP servers reload the glossary and produce
  expected top-k results. Use after every edit to multilingual-glossary.yaml.
argument-hint: "<source-language-query> <expected-canonical-term>"
---

# RAG multilingual glossary verification

After adding or modifying a Polish / German term in `multilingual-glossary.yaml`,
verify that both servers expand the query correctly and return the expected
canonical English document.

> This skill complements `.github/skills/expand-rag-glossary/SKILL.md`, which
> covers the glossary edit itself. **Run expand-rag-glossary first**, then this
> skill to verify.

---

## Pre-conditions

- `multilingual-glossary.yaml` has been edited.
- The canonical version lives at `tools/rag/multilingual-glossary.yaml`.
- The mirror at `tools/rag-dotnet/multilingual-glossary.yaml` (used only by local `dotnet run`) is byte-identical.

If you edited the .NET mirror without copying back to canonical, you'll get drift. Sync:

```powershell
Copy-Item tools/rag/multilingual-glossary.yaml tools/rag-dotnet/multilingual-glossary.yaml -Force
```

---

## Steps

### 1. Restart both HTTP servers so the new glossary is loaded

```powershell
docker compose --profile rag-python-http up -d --force-recreate rag-python-http
docker compose --profile rag-dotnet-http up -d --force-recreate rag-dotnet-http
Start-Sleep 12
```

Both servers reload `multilingual-glossary.yaml` at startup; restarts pick up the new entry.

### 2. Query in source language and confirm expansion

Edit `tools/rag/compare_queries.py` to add the test query (use existing `ML-pl-*` / `ML-de-*` entries as templates), or call `probe_weights.py` ad-hoc:

```powershell
python tools/rag/probe_weights.py "<source-language-query>"
```

For example:

```powershell
python tools/rag/probe_weights.py "Jak są obsługiwane refresh tokeny w IAM?"
```

### 3. Verify expansion in the result

The top-5 from BOTH servers should contain the expected canonical English document (e.g. `docs/adr/0010/...` for IAM refresh tokens).

- **Top-1 matches expected on both** → glossary entry works. Done.
- **Top-1 mismatched but expected file in top-5** → expansion partially works. Run `compare_queries.py` for a wider check (other queries may be affected by the new entry).
- **Expected file not in top-5 on either server** → glossary entry isn't being applied. Continue to step 4.

### 4. Diagnose missing expansion

a. **Confirm the YAML is well-formed**:

   ```powershell
   docker exec ecommerceapp-rag-python-http-1 cat /app/multilingual-glossary.yaml | Select-String "<new-key>"
   docker exec ecommerceapp-rag-dotnet-http-1 cat /multilingual-glossary.yaml | Select-String "<new-key>"
   ```

   Both should output the new entry. If the .NET mount returns "no such file", check `docker-compose.yaml` for the mount path.

b. **Check expansion at the embed step** (Python only):

   ```powershell
   docker logs ecommerceapp-rag-python-http-1 2>&1 | Select-String -Pattern "glossary|expand" | Select-Object -Last 20
   ```

   Look for log lines mentioning the new term being substituted. If absent, the preprocessor isn't picking up the entry — restart again or check `cfg.GlossaryPath` in the server bootstrap.

c. **For .NET, ensure `GlossaryExpansionPreprocessor` is registered**: `tools/rag-dotnet/src/RagTools.Mcp/Program.cs` should call `services.AddSingleton<IQueryPreprocessor, GlossaryExpansionPreprocessor>()`. If missing, the entry will load but never be applied.

### 5. Regression check

After confirming the new entry works, run the full multilingual eval slice to ensure no existing PL / DE entries were broken:

```powershell
python tools/rag/compare_queries.py
# Check ML-pl-* and ML-de-* rows in docs/reports/rag-parity-audit-<date>.md
```

A previously-passing ML query starting to fail after your edit indicates an over-broad expansion (e.g. you mapped a Polish noun that collides with another canonical term). Narrow the glossary entry or split into multiple keys.

---

## Common mistakes

- **Editing the .NET mirror only**. The mounted HTTP container reads the canonical from `tools/rag/`. Only the local `dotnet run` reads the mirror. Drift = asymmetric multilingual behaviour.
- **Forgetting to restart**. Glossary is loaded at startup. Existing requests don't re-read it.
- **Mapping ambiguous terms** (e.g. "konto" in Polish — could mean "account profile" OR "user identity"). Always pair with a context-disambiguating term in the glossary value.
- **Adding entries without testing**. The next maintainer won't know whether a low-precision multilingual query is your entry's fault or a pre-existing issue.

---

## Related skills / docs

- `.github/skills/expand-rag-glossary/SKILL.md` — adding entries (use first)
- `.github/skills/rag-query-debug/SKILL.md` — broader debugging
- `tools/rag/multilingual-glossary.yaml` — canonical glossary
- `docs/rag/rag-architecture.md` §multilingual-expansion — how the preprocessor works
- `tools/rag/compare_queries.py` — multilingual evaluation queries
