using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Messaging.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Messaging;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Messaging
{
    public class CleanupJobsIntegrationTests : BcBaseTest<IOutboxRepository>
    {
        public CleanupJobsIntegrationTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task CleanupTasks_DeleteOnlyEligibleRows()
        {
            var old = DateTime.UtcNow.AddDays(-8);
            var recent = DateTime.UtcNow.AddDays(-1);
            var dispatchedOld = CreateOutbox("dispatched-old", OutboxStatus.Dispatched, old);
            var dispatchedRecent = CreateOutbox("dispatched-recent", OutboxStatus.Dispatched, recent);
            var pendingOld = CreateOutbox("pending-old", OutboxStatus.Pending, null);
            var runningOld = CreateOutbox("running-old", OutboxStatus.Running, null);
            var failedOld = CreateOutbox("failed-old", OutboxStatus.Failed, null);
            var deadLetterOld = CreateOutbox("dead-letter-old", OutboxStatus.DeadLetter, null);

            var outbox = GetRequiredService<IOutboxRepository>();
            await outbox.AddAsync(dispatchedOld, CancellationToken);
            await outbox.AddAsync(dispatchedRecent, CancellationToken);
            await outbox.AddAsync(pendingOld, CancellationToken);
            await outbox.AddAsync(runningOld, CancellationToken);
            await outbox.AddAsync(failedOld, CancellationToken);
            await outbox.AddAsync(deadLetterOld, CancellationToken);

            var inboxOld = CreateInbox(10001, old);
            var inboxRecent = CreateInbox(10002, recent);
            var context = GetRequiredService<IMessagingDbContext>();
            context.Inbox.Add(inboxOld);
            context.Inbox.Add(inboxRecent);
            await context.SaveChangesAsync(CancellationToken);

            var options = GetRequiredService<MessagingOptions>();
            options.OutboxRetention = TimeSpan.FromDays(7);
            options.InboxRetention = TimeSpan.FromDays(7);
            var outboxTask = GetRequiredService("OutboxCleanup");
            var inboxTask = GetRequiredService("InboxCleanup");
            await outboxTask.ExecuteAsync(new JobExecutionContext(null, "cleanup-outbox"), CancellationToken);
            await inboxTask.ExecuteAsync(new JobExecutionContext(null, "cleanup-inbox"), CancellationToken);

            var remainingOutbox = GetRequiredService<IMessagingDbContext>().Outbox.AsNoTracking().ToList();
            remainingOutbox.Select(x => x.MessageTypeKey).ShouldBe(new[]
            {
                "dispatched-recent", "pending-old", "running-old", "failed-old", "dead-letter-old"
            }, ignoreOrder: true);
            var remainingInbox = GetRequiredService<IMessagingDbContext>().Inbox.AsNoTracking().ToList();
            remainingInbox.Select(x => x.MessageId).ShouldBe(new[] { 10002L });
        }

        [Fact]
        public async Task OutboxCleanup_ExactRetentionBoundary_IsNotDeleted()
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var boundary = CreateOutbox("dispatched-boundary", OutboxStatus.Dispatched, cutoff);
            var outbox = GetRequiredService<IOutboxRepository>();
            await outbox.AddAsync(boundary, CancellationToken);

            var deleted = await outbox.DeleteDispatchedOlderThanAsync(cutoff, CancellationToken);

            deleted.ShouldBe(0);
            GetRequiredService<IMessagingDbContext>().Outbox.AsNoTracking()
                .Any(message => message.MessageTypeKey == "dispatched-boundary")
                .ShouldBeTrue();
        }

        private IScheduledTask GetRequiredService(string taskName)
            => GetRequiredService<System.Collections.Generic.IEnumerable<IScheduledTask>>()
                .Single(task => task.TaskName == taskName);

        private static OutboxMessage CreateOutbox(string key, OutboxStatus status, DateTime? dispatchedAt)
        {
            var message = OutboxMessage.Create(key, "{}");
            SetPrivateProperty(message, nameof(OutboxMessage.Status), status);
            if (dispatchedAt.HasValue)
                SetPrivateProperty(message, nameof(OutboxMessage.DispatchedAt), dispatchedAt);
            return message;
        }

        private static ProcessedMessage CreateInbox(long messageId, DateTime processedAt)
        {
            var message = ProcessedMessage.Create(messageId, "CleanupJobsIntegrationTests");
            SetPrivateProperty(message, nameof(ProcessedMessage.ProcessedAt), processedAt);
            return message;
        }

        private static void SetPrivateProperty<T>(T instance, string propertyName, object value)
        {
            typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
                .SetValue(instance, value);
        }
    }
}