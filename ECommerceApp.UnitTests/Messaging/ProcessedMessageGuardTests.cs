using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AwesomeAssertions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Messaging
{
    public class ProcessedMessageGuardTests
    {
        [Fact]
        public async Task TryMarkProcessedAsync_FirstCall_ReturnsTrueAndPersists()
        {
            var databaseName = Guid.NewGuid().ToString();
            var options = CreateOptions(databaseName);
            await using var primary = new MessagingDbContext(options);
            await using var secondary = new MessagingDbContext(options);
            var services = new ServiceCollection()
                .AddSingleton(secondary)
                .BuildServiceProvider();
            var guard = new ProcessedMessageGuard(primary);

            await using var scope = await CrossContextTransactionScope.BeginAsync(primary, services);
            await guard.TryMarkProcessedAsync(42, "Test.Handler", new OutboxTransaction(scope));

            await using var verification = new MessagingDbContext(options);
            verification.Inbox.Should().ContainSingle();
            verification.Inbox.Single().MessageId.Should().Be(42);
            verification.Inbox.Single().HandlerType.Should().Be("Test.Handler");
        }

        [Fact]
        public async Task TryMarkProcessedAsync_TransactionlessFirstCall_ReturnsTrueAndPersists()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            await using var context = new MessagingDbContext(options);
            var guard = new ProcessedMessageGuard(context);

            (await guard.TryMarkProcessedAsync(42, "Test.Handler")).Should().BeTrue();
            context.Inbox.Should().ContainSingle();
        }

        [Fact]
        public async Task TryMarkProcessedAsync_DuplicateCall_ReturnsFalseNoSecondRow()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            await using var firstContext = new MessagingDbContext(options);
            await using var secondContext = new MessagingDbContext(options);
            var firstGuard = new ProcessedMessageGuard(firstContext);
            var secondGuard = new ProcessedMessageGuard(secondContext);

            (await firstGuard.TryMarkProcessedAsync(42, "Test.Handler")).Should().BeTrue();
            (await secondGuard.TryMarkProcessedAsync(42, "Test.Handler")).Should().BeFalse();

            secondContext.Inbox.Should().ContainSingle();
        }

        [Fact]
        public async Task TryMarkProcessedAsync_OtherDbException_Propagates()
        {
            var options = CreateOptions(Guid.NewGuid().ToString());
            var context = new MessagingDbContext(options);
            await context.DisposeAsync();
            var guard = new ProcessedMessageGuard(context);

            Func<Task> act = () => guard.TryMarkProcessedAsync(42, "Test.Handler");

            await act.Should().ThrowAsync<ObjectDisposedException>();
        }

        private static DbContextOptions<MessagingDbContext> CreateOptions(string databaseName)
            => new DbContextOptionsBuilder<MessagingDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;
    }
}
