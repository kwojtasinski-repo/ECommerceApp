# RAG tools container — Python + embedded Qdrant, no separate Qdrant server needed.
# See docs/rag/rag-architecture.md § 14 for the full design.
#
# Build:
#   docker build -t rag-tools tools/rag/
#
# Index docs (run after git clone or significant doc changes):
#   docker run --rm \
#     -v "%CD%:/workspace" \
#     -v qdrant_data:/data/qdrant \
#     -e RAG_WORKSPACE=/workspace \
#     rag-tools python ingest.py
#
# MCP server (VS Code starts this automatically via .vscode/mcp.json):
#   docker run --rm --interactive \
#     -v "%CD%:/workspace" \
#     -v qdrant_data:/data/qdrant \
#     -e RAG_WORKSPACE=/workspace \
#     -e PYTHONUNBUFFERED=1 \
#     rag-tools python mcp_server.py

FROM python:3.11-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Pre-download the embedding model so first ingest does not require internet access.
# Model: paraphrase-multilingual-MiniLM-L12-v2 (~500 MB, cached in this image layer).
RUN python -c "\
from sentence_transformers import SentenceTransformer; \
SentenceTransformer('sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2')"

# Bake in the Python scripts and config.
# The workspace (docs + .github) and Qdrant data are MOUNTED at runtime, not baked in.
COPY *.py .
COPY config.yaml .

# RAG_WORKSPACE tells common.py where the repo root is inside the container.
# Overridable at runtime via -e RAG_WORKSPACE=...
ENV RAG_WORKSPACE=/workspace
ENV PYTHONUNBUFFERED=1

# /data/qdrant — mount qdrant_data volume here (embedded Qdrant storage)
# /workspace    — mount repo root here (docs + .github + .cache for manifest)
VOLUME ["/data/qdrant", "/workspace"]
