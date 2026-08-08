using KgCodegen.Core.Cli;

try
{
    return CliRunner.Run(args, Console.Out, Console.Error);
}
catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException)
{
    // Missing or unreadable input files are a user error, not a bug: report them the
    // way the tool reports every other problem instead of dumping a stack trace.
    Console.Error.WriteLine($"error: {exception.Message}");
    return 1;
}
