using ECommerceApp.Domain.Shared;
using System;

namespace ECommerceApp.Domain.Sagas
{
    public class SagaInstance
    {
        public long Id { get; private set; }
        public string SagaType { get; private set; } = default!;
        public SagaInstanceStatus Status { get; private set; }
        public string CorrelationId { get; private set; } = default!;
        public DateTime CreatedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        private SagaInstance() { }

        public static SagaInstance Create(string sagaType, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(sagaType))
            {
                throw new DomainException("Saga type must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(correlationId))
            {
                throw new DomainException("Saga correlation id must not be empty.");
            }

            return new SagaInstance
            {
                SagaType = sagaType,
                CorrelationId = correlationId,
                Status = SagaInstanceStatus.Running,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void MarkCompleted()
        {
            EnsureStatus(SagaInstanceStatus.Running, nameof(MarkCompleted));
            Status = SagaInstanceStatus.Completed;
            CompletedAt = DateTime.UtcNow;
        }

        public void MarkCompensating()
        {
            EnsureStatus(SagaInstanceStatus.Running, nameof(MarkCompensating));
            Status = SagaInstanceStatus.Compensating;
        }

        public void MarkFailed()
        {
            if (Status != SagaInstanceStatus.Running && Status != SagaInstanceStatus.Compensating)
            {
                throw new DomainException($"Saga instance cannot be marked as failed from status '{Status}'.");
            }

            Status = SagaInstanceStatus.Failed;
            CompletedAt = DateTime.UtcNow;
        }

        private void EnsureStatus(SagaInstanceStatus expectedStatus, string operation)
        {
            if (Status != expectedStatus)
            {
                throw new DomainException($"Saga instance cannot execute '{operation}' from status '{Status}'.");
            }
        }
    }
}