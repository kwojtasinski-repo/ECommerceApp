using System.Diagnostics;

namespace KgCodegen.Tests;

public sealed class SubprocessSmokeTests
{
    [Fact]
    public async Task Actual_built_executable_runs_check_successfully_against_real_repo()
    {
        var root = FindRepositoryRoot();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(Path.Combine("tools", "kg", "kg-codegen", "KgCodegen"));
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--root");
        startInfo.ArgumentList.Add(root);
        startInfo.ArgumentList.Add("--check");

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var stdout = await process!.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("Module: 14", stdout);
        Assert.DoesNotContain("error:", stderr, StringComparison.OrdinalIgnoreCase);
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
