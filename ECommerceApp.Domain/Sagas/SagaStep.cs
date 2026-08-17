using ECommerceApp.Domain.Shared;
using System;

namespace ECommerceApp.Domain.Sagas
{
    public class SagaStep
    {
        public long Id { get; private set; }
        public long SagaInstanceId { get; private set; }
        public string StepName { get; private set; } = default!;
        public SagaStepStatus Status { get; private set; }
        public DateTime OccurredAt { get; private set; }
        public string Payload { get; private set; } = default!;

        private SagaStep() { }

        public static SagaStep Create(long sagaInstanceId, string stepName, string payload)
        {
            if (sagaInstanceId <= 0)
            {
                throw new DomainException("Saga instance id must be positive.");
            }

            if (string.IsNullOrWhiteSpace(stepName))
            {
                throw new DomainException("Saga step name must not be empty.");
            }

            if (payload is null)
            {
                throw new DomainException("Saga step payload must not be null.");
            }

            return new SagaStep
            {
                SagaInstanceId = sagaInstanceId,
                StepName = stepName,
                Payload = payload,
                Status = SagaStepStatus.Pending,
                OccurredAt = DateTime.UtcNow
            };
        }

        public void MarkCompleted()
        {
            EnsureStatus(SagaStepStatus.Pending, nameof(MarkCompleted));
            Status = SagaStepStatus.Completed;
        }

        public void MarkFailed()
        {
            EnsureStatus(SagaStepStatus.Pending, nameof(MarkFailed));
            Status = SagaStepStatus.Failed;
        }

        public void MarkCompensated()
        {
            EnsureStatus(SagaStepStatus.Completed, nameof(MarkCompensated));
            Status = SagaStepStatus.Compensated;
        }

        private void EnsureStatus(SagaStepStatus expectedStatus, string operation)
        {
            if (Status != expectedStatus)
            {
                throw new DomainException($"Saga step cannot execute '{operation}' from status '{Status}'.");
            }
        }
    }
}