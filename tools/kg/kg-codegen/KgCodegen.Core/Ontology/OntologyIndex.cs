namespace KgCodegen.Core.Ontology;

public sealed record OntologyIndex(IReadOnlySet<string> KnownLabels, IReadOnlySet<string> AllowedEdges);