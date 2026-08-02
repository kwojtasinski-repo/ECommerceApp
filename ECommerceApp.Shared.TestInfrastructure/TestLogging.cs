using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Microsoft.Extensions.Logging;
using System;

namespace ECommerceApp.Shared.TestInfrastructure
{
    public static class TestLogging
    {
        private static readonly ILoggerFactory TestcontainersLoggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(MinimumLevel);
        });

        public static LogLevel MinimumLevel => ResolveMinimumLevel();

        public static void Configure(ILoggingBuilder logging, XunitLogSink sink = null)
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddDebug();
            logging.SetMinimumLevel(MinimumLevel);

            if (sink != null)
            {
                logging.AddProvider(new XunitLoggerProvider(sink));
            }
        }

        public static ILogger CreateTestcontainersLogger()
            => TestcontainersLoggerFactory.CreateLogger("Testcontainers");

        public static IOutputConsumer CreateContainerOutputConsumer()
            => Consume.RedirectStdoutAndStderrToConsole();

        private static LogLevel ResolveMinimumLevel()
        {
            var configuredLevel = Environment.GetEnvironmentVariable("E2E_LOG_LEVEL");

            return Enum.TryParse(configuredLevel, ignoreCase: true, out LogLevel level)
                ? level
                : LogLevel.Debug;
        }
    }
}