namespace RagTools.Core.ReadDocs;

public sealed record ReadDocsRequest(
    string Collection,
    string Question,
    string? Topic = null,
    int TopFiles = 3);
