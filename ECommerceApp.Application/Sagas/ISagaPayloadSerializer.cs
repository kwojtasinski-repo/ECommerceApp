using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Sagas
{
    public interface ISagaPayloadSerializer
    {
        string Serialize(IMessage message);
        IMessage Deserialize(string payload, Type messageType);
    }
}