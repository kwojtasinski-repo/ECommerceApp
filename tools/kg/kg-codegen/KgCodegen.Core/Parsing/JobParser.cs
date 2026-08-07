using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

public sealed class JobParser(ModuleResolver modules)
{
    public ParserResult Parse(
        string applicationRoot,
        IReadOnlyList<CypherNode> actions,
        IReadOnlyList<CypherNode> messageHandlers,
        IReadOnlyList<CypherNode> messages,
        IReadOnlyList<CypherNode> repositories,
        IReadOnlyList<CypherEdge> persistedByEdges)
    {
        var graph = Graph.Empty();
        var warnings = new List<string>();
        var files = Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
            .Select(file => (Path: file, Root: CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot()))
            .ToArray();
        var jobs = new List<(string Path, CompilationUnitSyntax Root, ClassDeclarationSyntax Type, string Id)>();

        foreach (var file in files)
        {
            foreach (var type in file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (type.BaseList?.Types.Any(baseType => baseType.Type.ToString().Equals("IScheduledTask", StringComparison.Ordinal)) != true)
                {
                    continue;
                }

                var id = SyntaxNaming.FullyQualifiedName(file.Root, type);
                var taskName = ResolveTaskName(type, id, warnings);
                graph.Nodes.Add(new CypherNode("Job", id, new Dictionary<string, object?>
                {
                    ["taskName"] = taskName,
                    ["triggerMode"] = null
                }));
                var module = modules.Resolve(Path.GetRelativePath(applicationRoot, file.Path));
                if (module is not null)
                {
                    graph.Edges.Add(new CypherEdge("CONTAINS", "Module", module, "Job", id));
                }

                jobs.Add((file.Path, file.Root, type, id));
            }
        }

        var jobByName = jobs.ToDictionary(job => job.Type.Identifier.Text, StringComparer.Ordinal);
        var actionIds = actions.Where(node => node.Label.Equals("Action", StringComparison.Ordinal)).Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var handlerIds = messageHandlers.Where(node => node.Label.Equals("MessageHandler", StringComparison.Ordinal)).Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var messageResolver = new MessageNameResolver(messages);

        foreach (var file in files)
        {
            foreach (var invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                    !memberAccess.Name.Identifier.Text.Equals("ScheduleAsync", StringComparison.Ordinal) ||
                    memberAccess.Expression is not IdentifierNameSyntax receiver)
                {
                    continue;
                }

                var enclosingClass = invocation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                var enclosingMethod = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (enclosingClass is null || enclosingMethod is null || !HasField(enclosingClass, receiver.Identifier.Text, "IDeferredJobScheduler"))
                {
                    continue;
                }

                var target = ResolveJobTarget(invocation, jobByName);
                if (target is null)
                {
                    warnings.Add($"Could not resolve scheduled job in {file.Path}: {invocation}.");
                    continue;
                }

                var classId = SyntaxNaming.FullyQualifiedName(file.Root, enclosingClass);
                var sourceId = handlerIds.Contains(classId)
                    ? classId
                    : actionIds.Contains(classId + "." + enclosingMethod.Identifier.Text)
                        ? classId + "." + enclosingMethod.Identifier.Text
                        : null;
                var sourceLabel = handlerIds.Contains(classId) ? "MessageHandler" : "Action";
                if (sourceId is null)
                {
                    continue;
                }

                var edge = new CypherEdge("SCHEDULES", sourceLabel, sourceId, "Job", target.Id);
                if (!graph.Edges.Contains(edge))
                {
                    graph.Edges.Add(edge);
                    SetTriggerMode(graph, target.Id);
                }
            }
        }

        foreach (var job in jobs)
        {
            foreach (var field in job.Type.Members.OfType<FieldDeclarationSyntax>())
            {
                var interfaceName = GetDeclaredTypeName(field);
                if (interfaceName is null || !interfaceName.StartsWith("I", StringComparison.Ordinal) || !interfaceName.EndsWith("Repository", StringComparison.Ordinal))
                {
                    continue;
                }

                var repository = repositories.FirstOrDefault(node => node.Label.Equals("Repository", StringComparison.Ordinal) && node.Id.Split('.').Last().Equals(interfaceName, StringComparison.Ordinal));
                if (repository is null)
                {
                    warnings.Add($"Could not resolve repository interface '{interfaceName}' for job {job.Id}.");
                    continue;
                }

                foreach (var persistedBy in persistedByEdges.Where(edge => edge.Type.Equals("PERSISTED_BY", StringComparison.Ordinal) && edge.TargetId.Equals(repository.Id, StringComparison.Ordinal)))
                {
                    var edge = new CypherEdge("OPERATES_ON", "Job", job.Id, "Entity", persistedBy.SourceId);
                    if (!graph.Edges.Contains(edge))
                    {
                        graph.Edges.Add(edge);
                    }
                }
            }

            foreach (var field in job.Type.Members.OfType<FieldDeclarationSyntax>().Where(field => (GetDeclaredTypeName(field) ?? "").Equals("IOutboxWriter", StringComparison.Ordinal)))
            {
                var fieldName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text;
                if (fieldName is null)
                {
                    continue;
                }

                foreach (var method in job.Type.Members.OfType<MethodDeclarationSyntax>())
                {
                    foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                            !memberAccess.Expression.ToString().Equals(fieldName, StringComparison.Ordinal) ||
                            (!memberAccess.Name.Identifier.Text.Equals("EnqueueAsync", StringComparison.Ordinal) && !memberAccess.Name.Identifier.Text.Equals("PublishAsync", StringComparison.Ordinal)))
                        {
                            continue;
                        }

                        var resolver = new MessageNameResolver(messages);
                        var resolved = OutboxPublishResolver.ResolvePublishedMessage(invocation, method, job.Id, resolver, job.Root, out var warning);
                        if (warning is not null)
                        {
                            warnings.Add(warning);
                        }

                        if (resolved is null || !messages.Any(message => message.Id.Equals(resolved, StringComparison.Ordinal)))
                        {
                            continue;
                        }

                        var edge = new CypherEdge("PUBLISHES", "Job", job.Id, "Message", resolved);
                        if (!graph.Edges.Contains(edge))
                        {
                            graph.Edges.Add(edge);
                        }
                    }
                }
            }
        }

        foreach (var job in jobs.Where(job => !graph.Edges.Any(edge => edge.Type.Equals("SCHEDULES", StringComparison.Ordinal) && edge.TargetId.Equals(job.Id, StringComparison.Ordinal))))
        {
            warnings.Add($"Could not statically determine trigger mode for job {job.Id}.");
        }

        return new ParserResult(graph, warnings);
    }

    private static string? ResolveTaskName(ClassDeclarationSyntax type, string jobId, List<string> warnings)
    {
        var property = type.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault(member => member.Identifier.Text.Equals("TaskName", StringComparison.Ordinal));
        if (property?.ExpressionBody?.Expression is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText;
        }

        if (property?.ExpressionBody?.Expression is IdentifierNameSyntax identifier)
        {
            var constant = type.Members.OfType<FieldDeclarationSyntax>()
                .SelectMany(field => field.Declaration.Variables.Select(variable => (field, variable)))
                .FirstOrDefault(item => item.variable.Identifier.Text.Equals(identifier.Identifier.Text, StringComparison.Ordinal));
            if (constant.variable is not null && constant.variable.Initializer?.Value is LiteralExpressionSyntax constantLiteral)
            {
                return constantLiteral.Token.ValueText;
            }
        }

        warnings.Add($"Could not resolve TaskName for job {jobId}.");
        return null;
    }

    private static bool HasField(ClassDeclarationSyntax type, string fieldName, string declaredType)
    {
        return type.Members.OfType<FieldDeclarationSyntax>().Any(field =>
            GetDeclaredTypeName(field).Equals(declaredType, StringComparison.Ordinal) &&
            field.Declaration.Variables.Any(variable => variable.Identifier.Text.Equals(fieldName, StringComparison.Ordinal)));
    }

    private static string GetDeclaredTypeName(FieldDeclarationSyntax field) => field.Declaration.Type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        _ => field.Declaration.Type.ToString()
    };

    private static JobTarget? ResolveJobTarget(InvocationExpressionSyntax invocation, IReadOnlyDictionary<string, (string Path, CompilationUnitSyntax Root, ClassDeclarationSyntax Type, string Id)> jobs)
    {
        var expression = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression as MemberAccessExpressionSyntax;
        if (expression?.Name.Identifier.Text.Equals("JobTaskName", StringComparison.Ordinal) != true || expression.Expression is not IdentifierNameSyntax typeName)
        {
            return null;
        }

        return jobs.TryGetValue(typeName.Identifier.Text, out var job) ? new JobTarget(job.Id) : null;
    }

    private static void SetTriggerMode(Graph graph, string jobId)
    {
        var node = graph.Nodes.First(item => item.Label.Equals("Job", StringComparison.Ordinal) && item.Id.Equals(jobId, StringComparison.Ordinal));
        var properties = new Dictionary<string, object?>(node.Properties) { ["triggerMode"] = "Deferred" };
        graph.Nodes[graph.Nodes.IndexOf(node)] = node with { Properties = properties };
    }

    private sealed record JobTarget(string Id);
}