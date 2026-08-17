using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sagas;
using System;
using System.Text.Json;

namespace ECommerceApp.Infrastructure.Sagas
{
    internal sealed class SagaPayloadSerializer : ISagaPayloadSerializer
    {
        public string Serialize(IMessage message)
        {
            return JsonSerializer.Serialize(message, message.GetType());
        }

        public IMessage Deserialize(string payload, Type messageType)
        {
            return (IMessage)(JsonSerializer.Deserialize(payload, messageType)
                ?? throw new InvalidOperationException(
                    $"Saga payload deserialization returned null for '{messageType.FullName}'."));
        }
    }
}