using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Sagas;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sagas
{
    public interface ISagaDefinition
    {
        string SagaType { get; }
        IReadOnlyList<ISagaStepSpec> Steps { get; }
        Func<SagaTransitionContext, IMessage>? CompensationFactory { get; }
    }
}