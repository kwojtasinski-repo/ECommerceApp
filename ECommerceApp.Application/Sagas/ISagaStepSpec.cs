using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Sagas
{
    public interface ISagaStepSpec
    {
        Type MessageType { get; }
        string StepName { get; }
        SagaTransitionKind Kind { get; }
        bool StartsNewInstance { get; }
        Func<IMessage, string> ExtractCorrelationId { get; }
    }
}