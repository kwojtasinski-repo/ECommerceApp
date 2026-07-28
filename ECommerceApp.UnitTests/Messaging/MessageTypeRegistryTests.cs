using AwesomeAssertions;
using ECommerceApp.Application.Messaging;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Messaging
{
    public class MessageTypeRegistryTests
    {
        // Throwaway marker types local to this test class, registered under unique keys so
        // repeated/parallel test runs never collide with each other or with production entries.
        private sealed class TestMessageA { }
        private sealed class TestMessageB { }
        private sealed class UnregisteredTestMessage { }

        [Fact]
        public void KeyFor_UnregisteredType_Throws()
        {
            Action act = () => MessageTypeRegistry.KeyFor(typeof(UnregisteredTestMessage));

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void TypeFor_UnknownKey_Throws()
        {
            Action act = () => MessageTypeRegistry.TypeFor("no-such-outbox-message-key");

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void KeyFor_RegisteredType_RoundTripsThroughTypeFor()
        {
            var key = $"test-message-a-{Guid.NewGuid():N}";
            MessageTypeRegistry.Register(typeof(TestMessageA), key);

            var resolvedKey = MessageTypeRegistry.KeyFor(typeof(TestMessageA));
            var resolvedType = MessageTypeRegistry.TypeFor(key);

            resolvedKey.Should().Be(key);
            resolvedType.Should().Be(typeof(TestMessageA));
        }
    }
}
