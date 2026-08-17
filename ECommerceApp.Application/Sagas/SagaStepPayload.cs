using System;

namespace ECommerceApp.Application.Sagas
{
    public sealed class SagaStepPayload
    {
        public SagaStepPayload(string stepName, Type messageType, string payload)
        {
            StepName = stepName;
            MessageType = messageType;
            Payload = payload;
        }

        public string StepName { get; }
        public Type MessageType { get; }
        public string Payload { get; }
    }
}