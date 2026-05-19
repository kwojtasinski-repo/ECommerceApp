namespace RagTools.Core;

/// <summary>
/// Abstraction over a token counter and encoder, so both BertTokenCounter (BERT WordPiece, for tests)
/// and SentencePieceTokenCounter (for production embedding) can be used interchangeably.
/// </summary>
public interface ITokenCounter
{
    int Count(string text);
    IReadOnlyList<int> EncodeToIds(string text, int maxLength = 512);
}
