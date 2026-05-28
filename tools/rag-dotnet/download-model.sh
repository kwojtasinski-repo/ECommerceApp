#!/usr/bin/env bash
# Downloads pre-exported ONNX assets for paraphrase-multilingual-MiniLM-L12-v2.
#
# Cross-platform companion to download-model.ps1 (macOS / Linux).
# 100% curl — no Python, no venv, no optimum-cli required.
#
# Usage:
#   bash tools/rag-dotnet/download-model.sh
#
# Files written (gitignored):
#   tools/rag-dotnet/model/model.onnx               (~470 MB)
#   tools/rag-dotnet/model/vocab.txt                (~250 KB  — BERT uncased)
#   tools/rag-dotnet/model/tokenizer.json           (~17 MB)
#   tools/rag-dotnet/model/config.json
#   tools/rag-dotnet/model/sentencepiece.bpe.model

set -euo pipefail

BASE='https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/resolve/main'
# NOTE: this model uses SentencePiece tokenization (XLM-RoBERTa based) and has
# NO vocab.txt. The .NET BertTokenizer requires a WordPiece vocab.txt, so we
# pull the BERT-base-uncased vocabulary. See tools/rag-dotnet/README.md.
BERT_BASE='https://huggingface.co/bert-base-uncased/resolve/main'

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
OUT_DIR="$SCRIPT_DIR/model"
mkdir -p "$OUT_DIR"

# ── Color helpers ────────────────────────────────────────────────────────────
if [[ -t 1 ]]; then
  C_GRAY='\033[0;90m'; C_GREEN='\033[0;32m'; C_CYAN='\033[0;36m'; C_RESET='\033[0m'
else
  C_GRAY=''; C_GREEN=''; C_CYAN=''; C_RESET=''
fi

download() {
  local url="$1" name="$2" dest="$OUT_DIR/$name"
  if [[ -f "$dest" ]]; then
    local size
    size=$(du -sh "$dest" 2>/dev/null | cut -f1 || echo "?")
    printf "${C_GRAY}[skip] %s already exists (%s)${C_RESET}\n" "$name" "$size"
    return
  fi
  printf "[download] %s\n" "$url"
  curl -fSL --progress-bar -o "$dest" "$url"
  local size
  size=$(du -sh "$dest" 2>/dev/null | cut -f1 || echo "?")
  printf "${C_GREEN}[done]     %s — %s${C_RESET}\n" "$name" "$size"
}

download "$BASE/onnx/model.onnx"             "model.onnx"
download "$BERT_BASE/vocab.txt"              "vocab.txt"
download "$BASE/tokenizer.json"              "tokenizer.json"
download "$BASE/config.json"                 "config.json"
download "$BASE/sentencepiece.bpe.model"     "sentencepiece.bpe.model"

printf "\n${C_CYAN}Model ready in: %s${C_RESET}\n" "$OUT_DIR"
