using ECommerceApp.Application.Sales.Fulfillment.Services;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Domain.Sagas;
using ECommerceApp.Domain.Messaging;
using ECommerceApp.Domain.Sales.Fulfillment;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Infrastructure.Sagas;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.E2E.Backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.E2E.Backend.Sales.Fulfillment
{
    [Collection(MsSqlE2EFixture.CollectionName)]
    public class RefundSagaE2ETests : IClassFixture<MessageProcessingOperationsFixture>
    {
        private readonly MsSqlE2EFixture _sqlFixture;
        private readonly MessageProcessingOperationsFixture _messageProcessing;

        public RefundSagaE2ETests(
            MsSqlE2EFixture sqlFixture,
            MessageProcessingOperationsFixture messageProcessing)
        {
            _sqlFixture = sqlFixture;
            _messageProcessing = messageProcessing;
        }

        [Fact]
        public async Task ApproveRefundAsync_RefundWithStockItem_CompletesSagaAndReturnsStock()
        {
            var productId = TestIds.Next();
            const int refundedQuantity = 3;
            const int initialQuantity = 10;

            var scenario = await SeedRefundScenarioAsync(productId, initialQuantity, refundedQuantity);
            var finalSaga = await _messageProcessing.WaitUntilAsync(
                new SagaStatusOperation(_sqlFixture, "Refund", scenario.RefundId.ToString()),
                cancellationToken: TestContext.Current.CancellationToken);
            finalSaga.ShouldBe(SagaInstanceStatus.Completed);

            using var assertionScope = _sqlFixture.Services.CreateScope();
            var finalStock = await assertionScope.ServiceProvider
                .GetRequiredService<IStockService>()
                .GetByProductIdAsync(scenario.ProductId, TestContext.Current.CancellationToken);

            finalStock.ShouldNotBeNull();
            finalStock!.AvailableQuantity.ShouldBe(initialQuantity + refundedQuantity);
        }

        private async Task<RefundScenario> SeedRefundScenarioAsync(
            int productId,
            int initialQuantity,
            int refundedQuantity)
        {
            using var scope = _sqlFixture.Services.CreateScope();
            await _sqlFixture.EnsureTestCustomerAsync();

            var stockService = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stockService.InitializeStockAsync(productId, initialQuantity, TestContext.Current.CancellationToken);

            var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var order = Order.Create(
                1,
                1,
                new OrderUserId(MsSqlE2EFixture.TestCustomerId),
                OrderNumber.Generate(),
                new OrderCustomer(
                    "Jan",
                    "Kowalski",
                    "jan@test.com",
                    "123456789",
                    false,
                    null,
                    null,
                    "Główna",
                    "1",
                    null,
                    "67-100",
                    "Nowa Sól",
                    "Polska"));
            var orderId = await orderRepository.AddAsync(order, TestContext.Current.CancellationToken);

            var refundRepository = scope.ServiceProvider.GetRequiredService<IRefundRepository>();
            var refund = Refund.Create(
                orderId,
                "E2E refund",
                onWarranty: false,
                new[] { RefundItem.Create(productId, refundedQuantity) },
                $"refund-e2e-{Guid.NewGuid():N}");
            var refundId = await refundRepository.AddAsync(refund, TestContext.Current.CancellationToken);

            var refundService = scope.ServiceProvider.GetRequiredService<IRefundService>();
            var result = await refundService.ApproveRefundAsync(refundId, TestContext.Current.CancellationToken);
            result.ShouldBe(RefundOperationResult.Success);
            return new RefundScenario(productId, refundId);
        }

        private sealed record RefundScenario(int ProductId, int RefundId);

        private sealed class SagaStatusOperation : IMessageProcessingOperation<SagaInstanceStatus>
        {
            private readonly MsSqlE2EFixture _fixture;
            private readonly string _sagaType;
            private readonly string _correlationId;
            private string _diagnostics = string.Empty;

            public SagaStatusOperation(
                MsSqlE2EFixture fixture,
                string sagaType,
                string correlationId)
            {
                _fixture = fixture;
                _sagaType = sagaType;
                _correlationId = correlationId;
            }

            public async Task<SagaInstanceStatus> ReadAsync(CancellationToken cancellationToken)
            {
                using var scope = _fixture.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SagasDbContext>();
                var saga = await context.Sagas
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        instance => instance.SagaType == _sagaType
                            && instance.CorrelationId == _correlationId,
                        cancellationToken);
                if (saga is not null)
                {
                    var steps = await context.Steps
                        .AsNoTracking()
                        .Where(step => step.SagaInstanceId == saga.Id)
                        .OrderBy(step => step.Id)
                        .Select(step => $"{step.StepName}:{step.Status}")
                        .ToListAsync(cancellationToken);
                    _diagnostics = string.Join(", ", steps);
                }
                var outbox = await scope.ServiceProvider
                    .GetRequiredService<MessagingDbContext>()
                    .Outbox
                    .AsNoTracking()
                    .OrderByDescending(message => message.Id)
                    .Take(8)
                    .Select(message => $"{message.Id}:{message.MessageTypeKey}:{message.Status}:{message.RetryCount}:{message.ErrorMessage}")
                    .ToListAsync(cancellationToken);
                _diagnostics = $"{_diagnostics} Outbox: {string.Join(", ", outbox)}";
                return saga?.Status ?? SagaInstanceStatus.Running;
            }

            public bool IsCompleted(SagaInstanceStatus state)
                => state == SagaInstanceStatus.Completed;

            public string Describe(SagaInstanceStatus state)
                => $"Saga {_sagaType}/{_correlationId} has status {state}, expected Completed. Steps: {_diagnostics}";
        }
    }
}
