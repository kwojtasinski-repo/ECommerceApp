using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace RagTools.Mcp.Routing;

/// <summary>
/// Route constraint applied to <c>{collection}</c> segments — accepts
/// <c>^[a-z0-9][a-z0-9_-]*$</c>. Rejecting at routing time gives a clean 404
/// without entering the action and removes duplicate regex checks from controllers.
///
/// Registered in <c>Program.cs</c> via
/// <c>AddRouting(o =&gt; o.ConstraintMap["collection"] = typeof(CollectionNameRouteConstraint))</c>
/// and consumed as <c>[HttpPost("/ingest/{collection:collection}/batch")]</c>.
/// </summary>
public sealed partial class CollectionNameRouteConstraint : IRouteConstraint
{
    private static readonly Regex Pattern = CollectionRegex();

    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        ArgumentNullException.ThrowIfNull(routeKey);
        ArgumentNullException.ThrowIfNull(values);

        if (!values.TryGetValue(routeKey, out var raw) || raw is null)
        {
            return false;
        }

        var value = Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture);
        return !string.IsNullOrEmpty(value) && Pattern.IsMatch(value);
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CollectionRegex();
}
