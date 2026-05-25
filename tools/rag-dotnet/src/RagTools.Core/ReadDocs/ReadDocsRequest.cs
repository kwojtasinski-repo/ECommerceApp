namespace RagTools.Core.ReadDocs;

public sealed record ReadDocsRequest(
    string Collection,
    string Question,
    string? Bc = null,
    int TopFiles = 3);
