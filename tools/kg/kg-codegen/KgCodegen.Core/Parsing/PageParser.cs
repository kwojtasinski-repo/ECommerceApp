using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

public sealed class PageParser
{
    public ParserResult Parse(string webRoot, DomainSymbolIndex applicationSymbols, IReadOnlyList<CypherNode> actions) =>
        ControllerParserSupport.Parse(webRoot, "WebHost", "Page", ControllerParserSupport.IsWebController, applicationSymbols, actions);
}