# Downloads pre-exported ONNX assets for paraphrase-multilingual-MiniLM-L12-v2.
#
# 100% PowerShell — no Python, no venv, no optimum-cli required.
# Pulls the model that was already exported by sentence-transformers maintainers
# and published to the HuggingFace ONNX subfolder.
#
# Usage:
#   pwsh tools/rag-dotnet/download-model.ps1
#
# Files written (gitignored):
#   tools/rag-dotnet/model/model.onnx     (~470 MB — multilingual model is large)
#   tools/rag-dotnet/model/vocab.txt      (~250 KB)
#   tools/rag-dotnet/model/tokenizer.json (~17 MB — sentencepiece vocab)
#   tools/rag-dotnet/model/config.json

$ErrorActionPreference = 'Stop'

$Base    = 'https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/resolve/main'
$OutDir  = Join-Path $PSScriptRoot 'model'

if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
}

# NOTE: paraphrase-multilingual-MiniLM-L12-v2 uses SentencePiece tokenization
# (XLM-RoBERTa based) and has NO vocab.txt in its HuggingFace repository.
# The .NET BertTokenizer requires a WordPiece vocab.txt, so we download the
# BERT base uncased vocabulary instead. See README.md for details.
$BertBase = 'https://huggingface.co/bert-base-uncased/resolve/main'

$Files = @(
    @{ Url = "$Base/onnx/model.onnx";           Name = 'model.onnx' }
    @{ Url = "$BertBase/vocab.txt";             Name = 'vocab.txt' }
    @{ Url = "$Base/tokenizer.json";            Name = 'tokenizer.json' }
    @{ Url = "$Base/config.json";               Name = 'config.json' }
    @{ Url = "$Base/sentencepiece.bpe.model";   Name = 'sentencepiece.bpe.model' }
)

foreach ($f in $Files) {
    $dest = Join-Path $OutDir $f.Name
    if (Test-Path $dest) {
        $sizeMb = [math]::Round((Get-Item $dest).Length / 1MB, 2)
        Write-Host "[skip] $($f.Name) already exists ($sizeMb MB)" -ForegroundColor DarkGray
        continue
    }
    Write-Host "[download] $($f.Url)"
    # Faster than Invoke-WebRequest for large files (no progress bar overhead).
    $progressPreference = 'SilentlyContinue'
    try {
        Invoke-WebRequest -Uri $f.Url -OutFile $dest -UseBasicParsing
    }
    finally {
        $progressPreference = 'Continue'
    }
    $sizeMb = [math]::Round((Get-Item $dest).Length / 1MB, 2)
    Write-Host "[done]     $($f.Name) — $sizeMb MB" -ForegroundColor Green
}

Write-Host ""
Write-Host "Model ready in: $OutDir" -ForegroundColor Cyan
Write-Host "Set RAG_MODEL_DIR to point .NET tools at this directory."
