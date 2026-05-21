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

# NOTE: paraphrase-multilingual-MiniLM-L12-v2 uses SentencePiece (XLM-RoBERTa)
# and has NO vocab.txt in its HuggingFace repo. The .NET BertTokenizer requires
# WordPiece vocab.txt, so we download the BERT base uncased vocabulary instead.
# This produces valid (non-random) embeddings with reduced semantic accuracy.
# The Python implementation uses the correct SentencePiece tokenizer.
ARG BERT_VOCAB=https://huggingface.co/bert-base-uncased/resolve/main/vocab.txt

WORKDIR /app/model
RUN curl -sSL -o model.onnx              "${MODEL_BASE}/onnx/model.onnx"          \
 && curl -sSL -o vocab.txt               "${BERT_VOCAB}"                           \
 && curl -sSL -o tokenizer.json          "${MODEL_BASE}/tokenizer.json"            \
 && curl -sSL -o config.json             "${MODEL_BASE}/config.json"               \
 && curl -sSL -o sentencepiece.bpe.model "${MODEL_BASE}/sentencepiece.bpe.model"

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
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime

# Copy ONNX model from downloader stage.
COPY --from=model-downloader /app/model /app/model

# Copy .NET published outputs.
COPY --from=builder /out/ingest /app/ingest
COPY --from=builder /out/mcp    /app/mcp

# No config baked in — this image is generic and shared across projects.
# Each project mounts its own config.yaml at runtime and sets RAG_CONFIG.
# Example: -v ./tools/rag-dotnet/config.yaml:/config.yaml:ro -e RAG_CONFIG=/config.yaml

# Environment defaults — override via docker run -e or docker-compose environment:.
ENV RAG_WORKSPACE=/workspace
ENV RAG_MODEL_DIR=/app/model
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Volumes:
#   /workspace  — repo root (mounted read-only for ingest + MCP)
#   /config.yaml (or any path) — project-specific config, path set via RAG_CONFIG
VOLUME ["/workspace"]

# Ports:
#   3001  — SSE/HTTP transport (MCP_TRANSPORT=sse). Not used in stdio mode.
EXPOSE 3001

# Default: run MCP server. Override CMD for indexing.
ENTRYPOINT []
CMD ["dotnet", "/app/mcp/mcp_server.dll"]
