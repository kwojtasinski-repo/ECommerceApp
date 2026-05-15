# generate-eval-questions

Generate an eval question template for a newly indexed documentation file that has no existing eval coverage.

## Input

A file path (relative to the repo root) of a document that was indexed in Qdrant but has no corresponding entry in `tools/rag/eval/questions.json`.

## Process

1. Read the file at the given path.
2. Extract the document title:
   - First, look for a `# ` heading on any line.
   - If none, use the filename without extension.
3. Generate a natural-language question a developer would ask to retrieve this document.
4. Return a JSON object in the format below.

## Output format

```json
{
  "q": "<generated question>",
  "expect_any": ["<relative folder or file path>"],
  "auto_generated": true,
  "reviewed": false
}
```

## Question generation rules by doc type

| Path pattern | Question style | Example |
|---|---|---|
| `docs/adr/XXXX/` | "How is the [title] designed?" | "How is the Pricing Strategy Pattern designed?" |
| `docs/architecture/` | "What does the [title] describe?" | "What does the bounded context map describe?" |
| `.github/context/agent-decisions.md` | "What prior agent corrections and decisions have been recorded?" | — |
| `.github/context/known-issues.md` | "What are the current known issues and blockers?" | — |
| `.github/context/project-state.md` | "What is the current state of each bounded context implementation?" | — |
| `docs/patterns/` | "How should the [title] pattern be applied?" | — |
| `docs/roadmap/` | "What is the roadmap or timeline for [title]?" | — |
| `docs/reference/` | "What does the [title] reference document contain?" | — |

## `expect_any` path rules

- **ADR files**: use the folder path `docs/adr/XXXX/` — not the exact filename. This matches the main ADR file and all its amendments.
- **All other files**: use the exact relative file path as returned by `path.relative_to(REPO_ROOT).as_posix()`.

## Example output

Input path: `docs/adr/0017/0017-refund-policy-rules.md`  
First heading: `# ADR-0017 — Refund Policy Rules`

```json
{
  "q": "How are the Refund Policy Rules designed?",
  "expect_any": ["docs/adr/0017/"],
  "auto_generated": true,
  "reviewed": false
}
```

## After output

The `/rag-sync` agent appends this object to the `questions` array in `tools/rag/eval/questions.json`.
It is shown to the user for confirmation before being used in eval scoring.
