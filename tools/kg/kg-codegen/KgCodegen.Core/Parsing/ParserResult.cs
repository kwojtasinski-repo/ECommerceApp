using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

public sealed record ParserResult(Graph Graph, IReadOnlyList<string> Warnings);