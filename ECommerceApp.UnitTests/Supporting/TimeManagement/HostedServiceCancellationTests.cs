using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Infrastructure.Supporting.TimeManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AwesomeAssertions;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.TimeManagement
{
    public class HostedServiceCancellationTests
    {
        [Fact]
        public async Task BackgroundMessageDispatcher_PreCanceledToken_CompletesWithoutException()
        {
            var service = new BackgroundMessageDispatcher(
                new MessageChannel(),
                new Mock<IServiceScopeFactory>().Object,
                NullLogger<BackgroundMessageDispatcher>.Instance);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await InvokeExecuteAsync(service, cancellation.Token);
        }

        [Fact]
        public async Task CronSchedulerService_PreCanceledToken_CompletesWithoutException()
        {
            var service = new CronSchedulerService(
                new Mock<IServiceScopeFactory>().Object,
                new JobTriggerChannel(),
                new Mock<IJobStatusMonitor>().Object,
                NullLogger<CronSchedulerService>.Instance);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await InvokeExecuteAsync(service, cancellation.Token);
        }

        [Fact]
        public async Task DeferredJobPollerService_PreCanceledToken_CompletesWithoutException()
        {
            var service = new DeferredJobPollerService(
                new Mock<IServiceScopeFactory>().Object,
                new JobTriggerChannel(),
                NullLogger<DeferredJobPollerService>.Instance);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await InvokeExecuteAsync(service, cancellation.Token);
        }

        [Fact]
        public async Task JobDispatcherService_PreCanceledToken_CompletesWithoutException()
        {
            var service = new JobDispatcherService(
                new JobTriggerChannel(),
                new Mock<IServiceScopeFactory>().Object,
                new InMemoryJobStatusMonitor(),
                NullLogger<JobDispatcherService>.Instance);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await InvokeExecuteAsync(service, cancellation.Token);
        }

        [Fact]
        public async Task BackgroundMessageDispatcher_HandlerCancelsWithOtherToken_ContinuesWithNextMessage()
        {
            var channel = new MessageChannel();
            var handled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var services = new ServiceCollection();
            services.AddSingleton<IMessageHandler<DispatcherTestMessage>>(
                new DispatcherTestHandler(handled));
            using var provider = services.BuildServiceProvider();
            var service = new BackgroundMessageDispatcher(
                channel,
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<BackgroundMessageDispatcher>.Instance);
            var testCancellation = TestContext.Current.CancellationToken;

            await channel.Writer.WriteAsync(new DispatcherTestMessage { Sequence = 1 }, testCancellation);
            await channel.Writer.WriteAsync(new DispatcherTestMessage { Sequence = 2 }, testCancellation);
            channel.Writer.TryComplete();

            await InvokeExecuteAsync(service, CancellationToken.None);

            handled.Task.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task JobDispatcherService_ActiveJobCanceledByHost_DoesNotRecordExecution()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IScheduledTask, HostCancellationTask>();
            using var provider = services.BuildServiceProvider();
            var monitor = new InMemoryJobStatusMonitor();
            var service = new JobDispatcherService(
                new JobTriggerChannel(),
                provider.GetRequiredService<IServiceScopeFactory>(),
                monitor,
                NullLogger<JobDispatcherService>.Instance);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var trigger = new JobTriggerRequest
            {
                JobName = "host-cancelled-job",
                Source = Domain.Supporting.TimeManagement.JobTriggerSource.Manual
            };

            var process = InvokePrivateAsync(service, "ProcessTriggerAsync", trigger, cancellation.Token);

            Func<Task> act = () => process;
            await act.Should().ThrowAsync<OperationCanceledException>();
            monitor.GetLatest(trigger.JobName).Should().BeNull();
        }

        private static Task InvokeExecuteAsync(object service, CancellationToken cancellationToken)
            => InvokePrivateAsync(service, "ExecuteAsync", cancellationToken);

        private static Task InvokePrivateAsync(object service, string methodName, params object[] arguments)
        {
            var method = service.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            return (Task)method.Invoke(service, arguments)!;
        }

        private sealed class DispatcherTestMessage : IMessage
        {
            public int Sequence { get; init; }
        }

        private sealed class DispatcherTestHandler : IMessageHandler<DispatcherTestMessage>
        {
            private readonly TaskCompletionSource<bool> _handled;
            private int _count;

            public DispatcherTestHandler(TaskCompletionSource<bool> handled)
            {
                _handled = handled;
            }

            public Task HandleAsync(DispatcherTestMessage message, CancellationToken ct = default)
            {
                if (Interlocked.Increment(ref _count) == 1)
                {
                    throw new OperationCanceledException(new CancellationToken(true));
                }

                _handled.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

        private sealed class HostCancellationTask : IScheduledTask
        {
            public string TaskName => "host-cancelled-job";

            public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
                => Task.FromCanceled(cancellationToken);
        }
    }
}