using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

public sealed class EndpointParser
{
    public ParserResult Parse(string apiRoot, DomainSymbolIndex applicationSymbols, IReadOnlyList<CypherNode> actions) =>
        ControllerParserSupport.Parse(apiRoot, "ApiHost", "Endpoint", ControllerParserSupport.IsApiController, applicationSymbols, actions);
}