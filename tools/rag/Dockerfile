# RAG tools container — Python + embedded Qdrant, no separate Qdrant server needed.
# See docs/rag/rag-architecture.md § 14 for the full design.
#
# Build:
#   docker build -t rag-tools tools/rag/
#
# Index docs (run after git clone or significant doc changes).
# Uses whole-workspace mount so ingest can scan all .md files under docs/ and .github/:
#   docker run --rm \
#     -v "%CD%:/workspace" \
#     -v qdrant_data:/data/qdrant \
#     -e RAG_WORKSPACE=/workspace \
#     rag-tools python ingest.py
#
# MCP server (VS Code starts this automatically via .vscode/mcp.json).
# Per-file mount strategy — only the 3 project-specific files are mounted.
# config.yaml is baked into the image; RAG_METADATA/QUERIES/MANIFEST tell the
# server where each file lives. No whole-workspace mount, no hardcoded paths:
#   docker run --rm --interactive \
#     -v "%CD%/tools/rag/metadata-rules.yaml:/data/metadata-rules.yaml:ro" \
#     -v "%CD%/tools/rag/queries.yaml:/data/queries.yaml:ro" \
#     -v "%CD%/.rag/manifest.json:/data/manifest.json:ro" \
#     -v qdrant_data:/data/qdrant \
#     -e RAG_METADATA=/data/metadata-rules.yaml \
#     -e RAG_QUERIES=/data/queries.yaml \
#     -e RAG_MANIFEST=/data/manifest.json \
#     -e PYTHONUNBUFFERED=1 \
#     -e VECTOR_MODE=local \
#     rag-tools python mcp_server.py
#
# Verify mount on startup: the server prints each file's path + size in bytes.
# Compare with: Get-Item tools\rag\metadata-rules.yaml | Select Length

# ── Stage 1: install dependencies + pre-download model ────────────────────────
# All pip build artefacts (wheel cache, setuptools, .dist-info metadata) stay
# in this stage and are never copied to the runtime image.
FROM python:3.13-slim AS builder

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt \
 && python -c "\
from sentence_transformers import SentenceTransformer; \
SentenceTransformer('sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2')"

# ── Stage 2: lean runtime image ───────────────────────────────────────────────
# Equivalent of aspnet:10.0-noble-chiseled for .NET — drop the build tools,
# keep only what the running process needs.
FROM python:3.13-slim

# Copy installed site-packages (torch, sentence-transformers, mcp, uvicorn, …).
# pip, wheel, and setuptools are NOT copied — they live only in the builder.
COPY --from=builder /usr/local/lib/python3.13/site-packages \
                    /usr/local/lib/python3.13/site-packages

# Copy pip-installed console scripts (uvicorn entry-point, huggingface-cli, …).
COPY --from=builder /usr/local/bin /usr/local/bin

# Copy cached embedding model (~500 MB in /root/.cache/huggingface/).
COPY --from=builder /root/.cache /root/.cache

WORKDIR /app

# Bake in the Python scripts and config.
# The workspace (docs + .github) and Qdrant data are MOUNTED at runtime, not baked in.
COPY *.py .
COPY config.yaml .

# RAG_WORKSPACE tells common.py where the repo root is inside the container.
# Overridable at runtime via -e RAG_WORKSPACE=...
# VECTOR_MODE=local means the embedded Qdrant library is used (no separate server).
# docker-compose overrides this to VECTOR_MODE=docker (separate Qdrant container).
ENV RAG_WORKSPACE=/workspace
ENV PYTHONUNBUFFERED=1
ENV VECTOR_MODE=local

# /data/qdrant — mount qdrant_data volume here (embedded Qdrant storage)
# /workspace    — mount repo root here (docs + .github + .cache for manifest)
VOLUME ["/data/qdrant", "/workspace"]
