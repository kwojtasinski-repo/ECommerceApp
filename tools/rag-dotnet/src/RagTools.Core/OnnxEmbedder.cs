using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace RagTools.Core;

/// <summary>
/// Generates sentence embeddings using an ONNX-exported sentence-transformers model.
/// Compatible with paraphrase-multilingual-MiniLM-L12-v2 (384 dims).
///
/// The ONNX model is expected at /app/model/ inside the Docker image:
///   /app/model/model.onnx   — ONNX graph
///   /app/model/vocab.txt    — WordPiece vocabulary (for BertTokenCounter)
///   /app/model/tokenizer/   — (optional) full tokenizer config
/// </summary>
public sealed class OnnxEmbedder : IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenCounter _tokenCounter;
    private readonly int _maxSeqLen;

    /// <summary>Output dimensionality of the embedding model (384 for MiniLM-L12).</summary>
    public int Dimensions { get; }

    private OnnxEmbedder(InferenceSession session, BertTokenCounter tokenCounter, int dimensions, int maxSeqLen)
    {
        _session = session;
        _tokenCounter = tokenCounter;
        Dimensions = dimensions;
        _maxSeqLen = maxSeqLen;
    }

    /// <summary>Creates an instance for unit testing, bypassing the ONNX file load.</summary>
    internal static OnnxEmbedder CreateForTesting(BertTokenCounter tokenCounter, int dimensions = 4, int maxSeqLen = 16)
    {
        // Passing a null InferenceSession is only safe for tests that do NOT call EmbedBatch/Embed.
        // Tests exercising Tokenize, NormaliseRows, or Flatten do not need a real session.
        return new OnnxEmbedder(null!, tokenCounter, dimensions, maxSeqLen);
    }

    public static OnnxEmbedder Load(string modelDir, int maxSeqLen = 512)
    {
        var onnxPath = Path.Combine(modelDir, "model.onnx");
        if (!File.Exists(onnxPath))
            throw new FileNotFoundException(
                $"model.onnx not found in model directory: {modelDir}", onnxPath);

        var sessionOptions = new SessionOptions
        {
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING,
        };

        var session = new InferenceSession(onnxPath, sessionOptions);

        // Infer output dimensions from the ONNX graph metadata.
        // sentence-transformers ONNX models expose 'sentence_embedding' output of shape [batch, dim].
        var outputMeta = session.OutputMetadata;
        var dims = outputMeta.ContainsKey("sentence_embedding")
            ? (int)outputMeta["sentence_embedding"].Dimensions[1]
            : 384; // safe default for MiniLM-L12

        var tokenCounter = BertTokenCounter.FromModelDir(modelDir);
        return new OnnxEmbedder(session, tokenCounter, dims, maxSeqLen);
    }

    /// <summary>
    /// Embeds a batch of texts. Returns L2-normalised vectors of shape [n, Dimensions].
    /// Uses mean-pooling over token embeddings (standard sentence-transformers approach).
    /// </summary>
    public float[][] EmbedBatch(IReadOnlyList<string> texts)
    {
        if (texts.Count == 0) return [];

        // Tokenize all texts into padded tensors.
        var (inputIds, attentionMasks, tokenTypeIds) = Tokenize(texts);
        var batchSize = texts.Count;
        var seqLen = inputIds.GetLength(1);

        var inputIdsTensor = new DenseTensor<long>(Flatten(inputIds), [batchSize, seqLen]);
        var attentionMaskTensor = new DenseTensor<long>(Flatten(attentionMasks), [batchSize, seqLen]);
        var tokenTypeIdsTensor = new DenseTensor<long>(Flatten(tokenTypeIds), [batchSize, seqLen]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor),
        };

        using var results = _session.Run(inputs);

        // Extract sentence_embedding if available; otherwise mean-pool last_hidden_state.
        var embeddings = ExtractEmbeddings(results, batchSize, seqLen, attentionMasks);
        return NormaliseRows(embeddings);
    }

    public float[] Embed(string text) => EmbedBatch([text])[0];

    // ── Private helpers ───────────────────────────────────────────────────────

    internal (long[,] ids, long[,] masks, long[,] typeIds) Tokenize(IReadOnlyList<string> texts)
    {
        // Minimal BERT WordPiece tokenization using BertTokenCounter's underlying tokenizer.
        // We re-use the tokenizer that the BertTokenCounter holds, but we need full token IDs.
        // Since BertTokenCounter wraps Microsoft.ML.Tokenizers.BertTokenizer, we call Encode directly.
        //
        // NOTE: Microsoft.ML.Tokenizers.BertTokenizer returns EncodingResult with IDs and offsets.
        // This is sufficient for basic BERT-style encoding. For production, use HuggingFace tokenizers via
        // a native binding or pre-tokenize texts in a pre-processing step.

        var encodedList = new List<long[]>();
        foreach (var text in texts)
        {
            // BertTokenCounter._tokenizer is internal — we use a separate tokenizer instance here.
            // The token IDs are what the ONNX model sees: [CLS] + tokens + [SEP].
            var tokenIds = _tokenCounter.EncodeToIds(text, _maxSeqLen);
            long[] tokens = tokenIds is not null
                ? tokenIds.Select(id => (long)id).ToArray()
                : [101L, 102L]; // fallback: [CLS][SEP] when no vocab.txt loaded
            encodedList.Add(tokens);
        }

        var seqLen = Math.Min(encodedList.Max(e => e.Length), _maxSeqLen);
        var n = texts.Count;
        var ids = new long[n, seqLen];
        var masks = new long[n, seqLen];
        var typeIds = new long[n, seqLen]; // all zeros for single-sentence

        for (var i = 0; i < n; i++)
        {
            var enc = encodedList[i];
            var len = Math.Min(enc.Length, seqLen);
            for (var j = 0; j < len; j++)
            {
                ids[i, j] = enc[j];
                masks[i, j] = 1;
            }
        }
        return (ids, masks, typeIds);
    }

    private static float[][] ExtractEmbeddings(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results,
        int batchSize, int seqLen, long[,] attentionMasks)
    {
        // Prefer sentence_embedding (optimum export with pooling).
        var sentenceEmb = results.FirstOrDefault(r => r.Name == "sentence_embedding");
        if (sentenceEmb is not null)
        {
            var tensor = sentenceEmb.AsTensor<float>();
            var dims = (int)tensor.Dimensions[1];
            var output = new float[batchSize][];
            for (var i = 0; i < batchSize; i++)
            {
                output[i] = new float[dims];
                for (var d = 0; d < dims; d++)
                    output[i][d] = tensor[i, d];
            }
            return output;
        }

        // Fallback: mean-pool last_hidden_state with attention mask.
        var hidden = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();
        var hiddenDim = (int)hidden.Dimensions[2];
        var pooled = new float[batchSize][];
        for (var i = 0; i < batchSize; i++)
        {
            pooled[i] = new float[hiddenDim];
            long tokenCount = 0;
            for (var j = 0; j < seqLen; j++)
            {
                if (attentionMasks[i, j] == 0) continue;
                tokenCount++;
                for (var d = 0; d < hiddenDim; d++)
                    pooled[i][d] += hidden[i, j, d];
            }
            if (tokenCount > 0)
                for (var d = 0; d < hiddenDim; d++)
                    pooled[i][d] /= tokenCount;
        }
        return pooled;
    }

    internal static float[][] NormaliseRows(float[][] embeddings)
    {
        foreach (var row in embeddings)
        {
            var norm = MathF.Sqrt(row.Sum(x => x * x));
            if (norm > 1e-8f)
                for (var i = 0; i < row.Length; i++)
                    row[i] /= norm;
        }
        return embeddings;
    }

    internal static long[] Flatten(long[,] arr)
    {
        var rows = arr.GetLength(0);
        var cols = arr.GetLength(1);
        var flat = new long[rows * cols];
        Buffer.BlockCopy(arr, 0, flat, 0, flat.Length * sizeof(long));
        return flat;
    }

    public void Dispose()
    {
        _session?.Dispose();
        // BertTokenCounter is no longer IDisposable (whitespace approximation, no unmanaged resources).
    }
}
