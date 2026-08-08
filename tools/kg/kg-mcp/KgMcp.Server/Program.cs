using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using KgMcp.Core;
using KgMcp.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

var neo4jUrl = Environment.GetEnvironmentVariable("KG_NEO4J_URL") ?? "bolt://localhost:7687";
builder.Services.AddSingleton(_ => new KgGraphService(neo4jUrl));
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<KgTools>();

var app = builder.Build();
Console.Error.WriteLine($"[kg-mcp] ready; transport=stdio; neo4j={neo4jUrl}; migrations=none; graph-load=external");
await app.RunAsync();
