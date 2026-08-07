using KgCodegen.Core.Cli;

namespace KgCodegen.Tests;

public sealed class CliRunnerTests
{
    [Fact]
    public void Check_flag_never_writes_a_file()
    {
        var root = FindRepositoryRoot();
        var outputDirectory = CreateTemporaryDirectory();
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exitCode = CliRunner.Run(
                ["--root", root, "--out-dir", outputDirectory, "--check"],
                stdout,
                stderr);

            Assert.Equal(0, exitCode);
            Assert.Empty(Directory.EnumerateFiles(outputDirectory, "*.cypher"));
            Assert.DoesNotContain("error:", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(outputDirectory, true);
        }
    }

    [Fact]
    public void Without_check_flag_writes_a_timestamped_file()
    {
        var root = FindRepositoryRoot();
        var outputDirectory = CreateTemporaryDirectory();
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exitCode = CliRunner.Run(
                ["--root", root, "--out-dir", outputDirectory],
                stdout,
                stderr);

            Assert.Equal(0, exitCode);
            var files = Directory.EnumerateFiles(outputDirectory, "*.cypher").ToArray();
            Assert.Single(files);
            Assert.Contains("Wrote ", stdout.ToString());
            Assert.Empty(stderr.ToString());
        }
        finally
        {
            Directory.Delete(outputDirectory, true);
        }
    }

    [Fact]
    public void Ontology_error_causes_nonzero_exit()
    {
        var root = FindRepositoryRoot();
        var ontologyPath = Path.Combine(root, "tools", "kg", "kg-codegen", "KgCodegen.Tests", "Fixtures", "broken-ontology.json");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(
            ["--root", root, "--ontology", ontologyPath, "--check"],
            stdout,
            stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("error:", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "kg-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
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

        throw new DirectoryNotFoundException("Could not locate ECommerceApp repository root.");
    }
}
