using System.Diagnostics;
using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace KgMcp.Tests;

/// <summary>
/// The real repository graph, in an ephemeral Neo4j.
///
/// <para><b>Why this exists alongside <see cref="Neo4jFixture"/>.</b> That fixture is hand-built so
/// each test can point at an exact node; it is precise but it only contains the shapes someone
/// thought to add. Two defects shipped past it for exactly that reason — a traversal that reported
/// a node once per path length was invisible because the only multi-hop shape in the fixture was a
/// straight chain. This fixture contributes the opposite property: every shape the real codebase
/// actually produces, without anyone having to anticipate it. The two are complements, and neither
/// replaces the other.</para>
///
/// <para><b>Why it generates rather than reads a seed.</b> Generated seeds are gitignored and
/// timestamped, so a test reading one would pass or fail depending on which developer's machine it
/// ran on. Generating one here makes the whole path source → parsers → Cypher → Neo4j → traversal a
/// single deterministic run, and means the load itself is an assertion: if <c>CypherEmitter</c>
/// ever emits <c>key: null</c> again, Neo4j rejects the statement and every test in this class
/// fails at startup rather than silently drifting.</para>
///
/// <para><b>Why a subprocess and not a project reference.</b> Referencing <c>KgCodegen.Core</c>
/// would be the obvious way to build the graph, and it is what this fixture did first. Smart App
/// Control on this machine blocks the copied <c>KgCodegen.Core.dll</c> with <c>0x800711C7</c>, so
/// every test failed at startup with a <c>FileLoadException</c>. Running the real executable is
/// both a way around that and the more faithful test — it is the same command a human runs.</para>
///
/// <para><b>Cost.</b> Roughly a minute — the codegen pass over the real solution dominates, not the
/// container. That is the price of the coverage; do not "optimise" it by pinning a seed file.</para>
/// </summary>
public sealed class RealGraphFixture : IAsyncLifetime
{
    private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5.26.29-community").Build();

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>Repository root the graph was generated from.</summary>
    public string RepositoryRoot { get; private set; } = "";

    /// <summary>Everything `kg-codegen` printed. Its per-label counts and `Edges:` total are the
    /// numbers a human sees on the console, so tests can compare them against what actually
    /// landed in the database instead of against a constant typed into a test file.</summary>
    public string CodegenOutput { get; private set; } = "";

    public async Task InitializeAsync()
    {
        RepositoryRoot = FindRepositoryRoot();

        var seedPath = Path.Combine(Path.GetTempPath(), $"kg-e2e-{Guid.NewGuid():N}.cypher");
        var (exitCode, stdout, stderr) = await RunCodegenAsync(seedPath);
        CodegenOutput = stdout;

        Assert.True(
            exitCode == 0,
            $"kg-codegen failed before the graph could be loaded (exit {exitCode}):{Environment.NewLine}{stderr}");

        try
        {
            await _container.StartAsync();
            await RunScriptAsync(File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "kg", "seed", "ontology.cypher")));
            await RunScriptAsync(File.ReadAllText(seedPath));
        }
        finally
        {
            File.Delete(seedPath);
        }
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>Generates a seed by running the real `kg-codegen` executable, exactly as
    /// `tools/kg/load-graph.ps1` expects a human to have done.</summary>
    private async Task<(int ExitCode, string Stdout, string Stderr)> RunCodegenAsync(string seedPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(Path.Combine("tools", "kg", "kg-codegen", "KgCodegen"));
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--root");
        startInfo.ArgumentList.Add(RepositoryRoot);
        startInfo.ArgumentList.Add("--out");
        startInfo.ArgumentList.Add(seedPath);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var stdout = process!.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(5));

        return (process.ExitCode, await stdout, await stderr);
    }

    /// <summary>Reads a value out of a `Label: count` line of the codegen summary, so a test can
    /// state "what the tool reported" without hardcoding the number.</summary>
    public int ReportedCount(string label)
    {
        var line = CodegenOutput
            .Split('\n')
            .Select(text => text.Trim())
            .FirstOrDefault(text => text.StartsWith($"{label}: ", StringComparison.Ordinal));

        Assert.True(line is not null, $"kg-codegen printed no '{label}: <count>' line.");
        return int.Parse(line!.Split(':')[1].Trim());
    }

    public async Task<IReadOnlyList<IRecord>> QueryAsync(string cypher, object? parameters = null)
    {
        await using var driver = GraphDatabase.Driver(ConnectionString, AuthTokens.None);
        await using var session = driver.AsyncSession();
        return await session.ExecuteReadAsync(async transaction =>
        {
            var cursor = await transaction.RunAsync(cypher, parameters ?? new { });
            return await cursor.ToListAsync();
        });
    }

    /// <summary>
    /// Runs a `.cypher` file the way `cypher-shell --file` would. The driver takes one statement per
    /// call, so full-line comments are dropped and the remainder is split into statements. Only
    /// *full-line* comments are stripped: treating `//` as a comment anywhere would corrupt any
    /// string value containing a URL.
    /// </summary>
    private async Task RunScriptAsync(string script)
    {
        var withoutComments = string.Join(
            "\n",
            script.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        var statements = SplitStatements(withoutComments)
            .Select(statement => statement.Trim())
            .Where(statement => statement.Length > 0)
            .ToList();

        await using var driver = GraphDatabase.Driver(ConnectionString, AuthTokens.None);
        await using var session = driver.AsyncSession();
        foreach (var statement in statements)
        {
            await session.ExecuteWriteAsync(async transaction =>
            {
                var cursor = await transaction.RunAsync(statement);
                await cursor.ConsumeAsync();
            });
        }

        await session.ExecuteWriteAsync(async transaction =>
        {
            var cursor = await transaction.RunAsync("CALL db.awaitIndexes()");
            await cursor.ConsumeAsync();
        });
    }

    /// <summary>
    /// Splits on `;` outside string literals. A plain `Split(';')` is wrong here and not
    /// theoretically: `ontology.cypher` carries semicolons inside several `description` values, and
    /// splitting through one produces two fragments that both fail to parse. Backslash escapes are
    /// honoured because `CypherEmitter` writes `\'` for an apostrophe.
    /// </summary>
    private static IEnumerable<string> SplitStatements(string script)
    {
        var statement = new System.Text.StringBuilder();
        var inString = false;

        for (var index = 0; index < script.Length; index++)
        {
            var character = script[index];

            if (inString)
            {
                statement.Append(character);
                if (character == '\\' && index + 1 < script.Length)
                {
                    statement.Append(script[++index]);
                }
                else if (character == '\'')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '\'')
            {
                inString = true;
                statement.Append(character);
            }
            else if (character == ';')
            {
                yield return statement.ToString();
                statement.Clear();
            }
            else
            {
                statement.Append(character);
            }
        }

        yield return statement.ToString();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ECommerceApp.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Could not find ECommerceApp.sln above {AppContext.BaseDirectory}.");
    }
}

[CollectionDefinition(Name)]
public sealed class RealGraphCollection : ICollectionFixture<RealGraphFixture>
{
    public const string Name = "real-graph";
}
