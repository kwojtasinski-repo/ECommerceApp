using AwesomeAssertions;
using ECommerceApp.Application.Constants;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Messaging;
using ECommerceApp.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Messaging
{
    public class OutboxDispatcherTests
    {
        private sealed class TestMessage : IMessage
        {
            public int Value { get; set; }
        }

        // MessageTypeRegistry.Register keys its internal dictionary by Type, so TestMessage can only
        // ever be registered once for the lifetime of the test process — register it exactly once in
        // a static constructor (a fresh Guid-suffixed key would still collide on the *type*, not just
        // the key, if registered per-test).
        private static readonly string TestMessageKey = $"outbox-dispatcher-tests-message-{Guid.NewGuid():N}";

        static OutboxDispatcherTests()
        {
            MessageTypeRegistry.Register(typeof(TestMessage), TestMessageKey);
        }

        private readonly Mock<IModuleClient> _moduleClient = new();
        private readonly Mock<IOutboxRepository> _outboxRepository = new();
        private readonly OutboxDispatcher _dispatcher;

        public OutboxDispatcherTests()
        {
            _dispatcher = new OutboxDispatcher(
                _moduleClient.Object,
                _outboxRepository.Object,
                Options.Create(new RetryPolicyOptions()),
                NullLogger<OutboxDispatcher>.Instance);
        }

        [Fact]
        public async Task DispatchAsync_ValidMessage_CallsModuleClientAndMarksDispatched()
        {
            var key = TestMessageKey;
            var payload = JsonSerializer.Serialize(new TestMessage { Value = 7 });
            var message = OutboxMessage.Create(key, payload);

            IMessage? published = null;
            _moduleClient
                .Setup(m => m.PublishAsync(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => published = m)
                .Returns(Task.CompletedTask);

            await _dispatcher.DispatchAsync(message, CancellationToken.None);

            _moduleClient.Verify(m => m.PublishAsync(It.IsAny<IMessage>()), Times.Once);
            published.Should().BeOfType<TestMessage>();
            ((TestMessage)published!).Value.Should().Be(7);
            message.Status.Should().Be(OutboxStatus.Dispatched);
            _outboxRepository.Verify(r => r.UpdateAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DispatchAsync_UnknownMessageTypeKey_MarksFailedNotThrow()
        {
            var message = OutboxMessage.Create($"unregistered-{Guid.NewGuid():N}", "{}");

            Func<Task> act = () => _dispatcher.DispatchAsync(message, CancellationToken.None);

            await act.Should().NotThrowAsync();
            _moduleClient.Verify(m => m.PublishAsync(It.IsAny<IMessage>()), Times.Never);
            message.Status.Should().Be(OutboxStatus.Pending);
            message.RetryCount.Should().Be(1);
            _outboxRepository.Verify(r => r.UpdateAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DispatchAsync_ModuleClientThrows_MarksFailedWithBackoff()
        {
            var key = TestMessageKey;
            var message = OutboxMessage.Create(key, JsonSerializer.Serialize(new TestMessage { Value = 3 }));

            _moduleClient
                .Setup(m => m.PublishAsync(It.IsAny<IMessage>()))
                .ThrowsAsync(new InvalidOperationException("handler exploded"));

            Func<Task> act = () => _dispatcher.DispatchAsync(message, CancellationToken.None);

            await act.Should().NotThrowAsync();
            message.Status.Should().Be(OutboxStatus.Pending);
            message.RetryCount.Should().Be(1);
            message.ErrorMessage.Should().Be("handler exploded");
            _outboxRepository.Verify(r => r.UpdateAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DispatchAsync_ExceedsMaxRetries_MarksDeadLetter()
        {
            var key = TestMessageKey;
            var message = OutboxMessage.Create(key, JsonSerializer.Serialize(new TestMessage { Value = 9 }), maxRetries: 0);

            _moduleClient
                .Setup(m => m.PublishAsync(It.IsAny<IMessage>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            await _dispatcher.DispatchAsync(message, CancellationToken.None);

            message.Status.Should().Be(OutboxStatus.DeadLetter);
            _outboxRepository.Verify(r => r.UpdateAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
