# ── Stage 1: ONNX model export (Python) ──────────────────────────────────────
# Exports the sentence-transformers model to ONNX format using optimum.
# The model is baked into the image layer so the .NET runtime doesn't need Python.
FROM python:3.12-slim AS model-exporter

RUN pip install --no-cache-dir \
    sentence-transformers==3.4.1 \
    optimum[onnxruntime]==1.23.3

# Export model to ONNX with sentence_embedding output node (includes mean-pooling).
# --task sentence-similarity adds the pooling + normalisation nodes to the graph.
RUN optimum-cli export onnx \
    --model sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2 \
    --task sentence-similarity \
    --opset 14 \
    /app/model

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

# Copy ONNX model from Python exporter stage.
COPY --from=model-exporter /app/model /app/model

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
ENV PYTHONUNBUFFERED=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Volumes:
#   /workspace  — repo root (mounted read-only for ingest + MCP)
#   /data/qdrant — NOT used by .NET (requires external Qdrant server via QDRANT_URL)
VOLUME ["/workspace"]

# Default: run MCP server. Override with 'ingest' for indexing.
ENTRYPOINT []
CMD ["dotnet", "/app/mcp/mcp_server.dll"]
