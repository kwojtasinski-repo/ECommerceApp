using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KgCodegen.Core.Parsing;

/// <summary>
/// Maps a Razor view to the controller actions that can render it under a name other than the
/// action's own.
///
/// Survey of `ECommerceApp.Web` controllers (every `View(...)` call): all but two shapes are the
/// convention `View(model)` / `View()`, where view name == action name and the Page id already
/// matches by filename. The exceptions are
/// - `ProductController.Create`/`Edit` (GET and POST): `View(flag ? CreateChunkedView : CreateView, m)`
///   with `private const string` operands (`"AddItemNew"` / `"EditItemNew"`),
/// - `CheckoutController.ResumeOrder`: `View(nameof(PlaceOrder), m)`.
/// The index is general: it reads a literal, a same-class `const string`, `nameof(X)` and a
/// conditional/parenthesised combination of those as the first `View(...)` argument.
///
/// Semantics: an entry means "this action *may* render this view". For a conditional both operands
/// are recorded, because which one is chosen depends on a runtime value (e.g. `UseChunkedUpload`)
/// that a syntax-only scan cannot evaluate. This is consistent with the ontology's `Page` marker
/// (MVC action + Razor view) and with `USES`, which is a structural "can use" fact, not a trace.
/// Page ids come from the same `ControllerParserSupport.EnumerateActionSites` enumeration as
/// `PageParser`, so `#N` overload suffixes line up exactly.
/// </summary>
internal sealed class RenderedViewIndex
{
    private readonly Dictionary<(string? Area, string Controller, string View), HashSet<string>> _pageIds;

    private RenderedViewIndex(Dictionary<(string? Area, string Controller, string View), HashSet<string>> pageIds)
    {
        _pageIds = pageIds;
    }

    public static RenderedViewIndex Build(string webRoot)
    {
        var index = new Dictionary<(string? Area, string Controller, string View), HashSet<string>>();
        var constantsByClass = new Dictionary<ClassDeclarationSyntax, Dictionary<string, string>>();
        foreach (var site in ControllerParserSupport.EnumerateActionSites(webRoot, ControllerParserSupport.IsWebController))
        {
            if (!TryReadAddress(site.ClassId, out var area, out var controller))
            {
                continue;
            }

            if (!constantsByClass.TryGetValue(site.Class, out var constants))
            {
                constants = ReadStringConstants(site.Class);
                constantsByClass[site.Class] = constants;
            }

            foreach (var invocation in site.Method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not IdentifierNameSyntax { Identifier.Text: "View" } ||
                    invocation.ArgumentList.Arguments.Count == 0 ||
                    invocation.ArgumentList.Arguments[0].NameColon is not null)
                {
                    continue;
                }

                foreach (var viewName in ResolveViewNames(invocation.ArgumentList.Arguments[0].Expression, constants))
                {
                    var key = (area, controller, viewName);
                    if (!index.TryGetValue(key, out var ids))
                    {
                        ids = new HashSet<string>(StringComparer.Ordinal);
                        index[key] = ids;
                    }

                    ids.Add(site.NodeId);
                }
            }
        }

        return new RenderedViewIndex(index);
    }

    public bool Renders(string pageId, string? area, string controller, string view) =>
        _pageIds.TryGetValue((area, controller, view), out var ids) && ids.Contains(pageId);

    /// <summary>
    /// Splits `Namespace.Areas.{Area}.Controllers.{Name}Controller` into area and controller name.
    /// Shared with `ScriptModuleParser.MatchesPage` so both agree on what a controller is called.
    /// </summary>
    public static bool TryReadAddress(string classId, out string? area, out string controller)
    {
        area = null;
        controller = string.Empty;
        var lastDot = classId.LastIndexOf('.');
        var className = lastDot >= 0 ? classId[(lastDot + 1)..] : classId;
        if (!className.EndsWith("Controller", StringComparison.Ordinal))
        {
            return false;
        }

        controller = className[..^"Controller".Length];
        var namespaceName = lastDot >= 0 ? classId[..lastDot] : string.Empty;
        const string AreasMarker = ".Areas.";
        const string ControllersSuffix = ".Controllers";
        var areasStart = namespaceName.LastIndexOf(AreasMarker, StringComparison.Ordinal);
        if (areasStart >= 0 && namespaceName.EndsWith(ControllersSuffix, StringComparison.Ordinal))
        {
            var areaStart = areasStart + AreasMarker.Length;
            var areaName = namespaceName[areaStart..^ControllersSuffix.Length];
            if (areaName.Length > 0 && !areaName.Contains('.', StringComparison.Ordinal))
            {
                area = areaName;
            }
        }

        return true;
    }

    private static Dictionary<string, string> ReadStringConstants(ClassDeclarationSyntax controller)
    {
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in controller.Members.OfType<FieldDeclarationSyntax>()
            .Where(field => field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ConstKeyword))))
        {
            foreach (var variable in field.Declaration.Variables)
            {
                if (variable.Initializer?.Value is LiteralExpressionSyntax literal &&
                    literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    constants[variable.Identifier.Text] = literal.Token.ValueText;
                }
            }
        }

        return constants;
    }

    private static IEnumerable<string> ResolveViewNames(ExpressionSyntax expression, IReadOnlyDictionary<string, string> constants)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                return [literal.Token.ValueText];
            case IdentifierNameSyntax identifier when constants.TryGetValue(identifier.Identifier.Text, out var value):
                return [value];
            case ParenthesizedExpressionSyntax parenthesized:
                return ResolveViewNames(parenthesized.Expression, constants);
            case ConditionalExpressionSyntax conditional:
                return ResolveViewNames(conditional.WhenTrue, constants)
                    .Concat(ResolveViewNames(conditional.WhenFalse, constants))
                    .ToArray();
            case InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } } nameOf
                when nameOf.ArgumentList.Arguments.Count == 1:
                return [LastIdentifier(nameOf.ArgumentList.Arguments[0].Expression)];
            default:
                return [];
        }
    }

    private static string LastIdentifier(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        _ => expression.ToString()
    };
}
