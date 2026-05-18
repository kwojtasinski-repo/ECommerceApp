# ── Stage 1: ONNX model download ──────────────────────────────────────────────
# Pulls the pre-exported ONNX bundle straight from HuggingFace — no Python,
# no optimum-cli, no venv. The model maintainers publish an /onnx/ subfolder
# with model.onnx + tokenizer assets ready to use.
#
# Curl is used for a small image (curlimages/curl is ~12 MB) and stable
# download progress in CI. Files mirror exactly what tools/rag-dotnet/download-model.ps1
# downloads for local development, so Docker and local devs run the same bytes.
FROM curlimages/curl:8.10.1 AS model-downloader

ARG MODEL_BASE=https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/resolve/main

WORKDIR /app/model
RUN curl -sSL -o model.onnx     "${MODEL_BASE}/onnx/model.onnx"     \
 && curl -sSL -o vocab.txt      "${MODEL_BASE}/vocab.txt"           \
 && curl -sSL -o tokenizer.json "${MODEL_BASE}/tokenizer.json"      \
 && curl -sSL -o config.json    "${MODEL_BASE}/config.json"

# ── Stage 2: .NET build ───────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /build
COPY src/ src/
COPY RagTools.sln .

RUN dotnet restore RagTools.sln
RUN dotnet publish src/RagTools.Ingest/RagTools.Ingest.csproj \
    -c Release -o /out/ingest --no-restore
RUN dotnet publish src/RagTools.Mcp/RagTools.Mcp.csproj \
    -c Release -o /out/mcp --no-restore

# ── Stage 3: Runtime image ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime

# Copy ONNX model from downloader stage.
COPY --from=model-downloader /app/model /app/model

# Copy .NET published outputs.
COPY --from=builder /out/ingest /app/ingest
COPY --from=builder /out/mcp    /app/mcp

# Copy config.yaml from the rag/ folder (shared between Python and .NET).
# Override at runtime by mounting your repo's config at /app/config.yaml.
COPY ../rag/config.yaml /app/config.yaml

# Environment defaults — override via docker run -e or docker-compose environment:.
ENV RAG_WORKSPACE=/workspace
ENV RAG_MODEL_DIR=/app/model
ENV RAG_CONFIG=/app/config.yaml
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Volumes:
#   /workspace  — repo root (mounted read-only for ingest + MCP)
#   /data/qdrant — NOT used by .NET (requires external Qdrant server via QDRANT_URL)
VOLUME ["/workspace"]

# Default: run MCP server. Override with 'ingest' for indexing.
ENTRYPOINT []
CMD ["dotnet", "/app/mcp/mcp_server.dll"]
