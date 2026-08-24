using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Domain.Sagas;
using ECommerceApp.E2E.Backend.Infrastructure;
using ECommerceApp.Infrastructure.Sagas;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.E2E.Backend.Inventory
{
    [Collection(MsSqlE2EFixture.CollectionName)]
    public sealed class AvailabilityReservationChangeSagaE2ETests : IClassFixture<MessageProcessingOperationsFixture>
    {
        private readonly MsSqlE2EFixture _sqlFixture;
        private readonly MessageProcessingOperationsFixture _messageProcessing;

        public AvailabilityReservationChangeSagaE2ETests(
            MsSqlE2EFixture sqlFixture,
            MessageProcessingOperationsFixture messageProcessing)
        {
            _sqlFixture = sqlFixture;
            _messageProcessing = messageProcessing;
        }

        [Fact]
        public async Task ReserveAsync_WhenAvailabilityDrops_CompletesSagaAndRemovesNewestActiveReservations()
        {
            var productId = Random.Shared.Next(1_000_000, int.MaxValue);
            var now = DateTime.UtcNow;

            await SeedScenarioAsync(productId, now);

            using (var scope = _sqlFixture.Services.CreateScope())
            {
                var result = await scope.ServiceProvider
                    .GetRequiredService<IStockService>()
                    .ReserveAsync(
                        new ReserveStockDto(
                            productId,
                            Random.Shared.Next(1_000_000, int.MaxValue),
                            1,
                            $"stock-holder-{Guid.NewGuid():N}",
                            now.AddHours(1)),
                        TestContext.Current.CancellationToken);
                result.ShouldBe(ReserveStockResult.Success);
            }

            var saga = await _messageProcessing.WaitUntilAsync(
                new AvailabilityScenarioCompletedOperation(_sqlFixture, productId),
                cancellationToken: TestContext.Current.CancellationToken);
            saga.ShouldBeTrue();

            using var assertionScope = _sqlFixture.Services.CreateScope();
            var reservations = await assertionScope.ServiceProvider
                .GetRequiredService<ISoftReservationRepository>()
                .GetByProductIdAsync(new PresaleProductId(productId), TestContext.Current.CancellationToken);

            reservations.Count.ShouldBe(3);
            reservations.ShouldContain(reservation => reservation.UserId.Value == "oldest-holder");
            reservations.ShouldContain(reservation => reservation.UserId.Value == "middle-holder");
            reservations.ShouldContain(reservation => reservation.UserId.Value == "committed-holder");
            reservations.ShouldNotContain(reservation => reservation.UserId.Value == "newest-holder");
            reservations.Single(reservation => reservation.UserId.Value == "committed-holder")
                .Status.ShouldBe(SoftReservationStatus.Committed);
        }

        private async Task SeedScenarioAsync(int productId, DateTime now)
        {
            using var scope = _sqlFixture.Services.CreateScope();
            var productSnapshots = scope.ServiceProvider.GetRequiredService<IProductSnapshotRepository>();
            await productSnapshots.UpsertAsync(
                ProductSnapshot.Create(productId, $"Product-{productId}", false, CatalogProductStatus.Orderable),
                TestContext.Current.CancellationToken);

            var stockService = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stockService.InitializeStockAsync(productId, 5, TestContext.Current.CancellationToken);

            await _messageProcessing.WaitUntilAsync(
                new StockSnapshotReadyOperation(_sqlFixture, productId),
                cancellationToken: TestContext.Current.CancellationToken);

            var reservations = scope.ServiceProvider.GetRequiredService<ISoftReservationRepository>();
            await reservations.AddAsync(
                SoftReservation.Create(productId, "oldest-holder", 2, 10m, now.AddMinutes(10)),
                TestContext.Current.CancellationToken);
            await reservations.AddAsync(
                SoftReservation.Create(productId, "middle-holder", 1, 10m, now.AddMinutes(15)),
                TestContext.Current.CancellationToken);
            await reservations.AddAsync(
                SoftReservation.Create(productId, "newest-holder", 2, 10m, now.AddMinutes(20)),
                TestContext.Current.CancellationToken);

            var committed = SoftReservation.Create(
                productId,
                "committed-holder",
                4,
                10m,
                now.AddMinutes(25));
            committed.Commit();
            await reservations.AddAsync(committed, TestContext.Current.CancellationToken);
        }

        private sealed class AvailabilityScenarioCompletedOperation : IMessageProcessingOperation<bool>
        {
            private readonly MsSqlE2EFixture _fixture;
            private readonly int _productId;

            public AvailabilityScenarioCompletedOperation(MsSqlE2EFixture fixture, int productId)
            {
                _fixture = fixture;
                _productId = productId;
            }

            public async Task<bool> ReadAsync(CancellationToken cancellationToken)
            {
                using var scope = _fixture.Services.CreateScope();
                var saga = await scope.ServiceProvider
                    .GetRequiredService<SagasDbContext>()
                    .Sagas
                    .AsNoTracking()
                    .Where(
                        instance => instance.SagaType == "AvailabilityReservationChange"
                            && instance.CorrelationId == _productId.ToString())
                    .OrderByDescending(instance => instance.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (saga?.Status != SagaInstanceStatus.Completed)
                {
                    return false;
                }

                var reservations = await scope.ServiceProvider
                    .GetRequiredService<ISoftReservationRepository>()
                    .GetByProductIdAsync(new PresaleProductId(_productId), cancellationToken);
                return reservations.Count == 3
                    && reservations.Any(reservation => reservation.UserId.Value == "oldest-holder")
                    && reservations.Any(reservation => reservation.UserId.Value == "middle-holder")
                    && reservations.Any(reservation => reservation.UserId.Value == "committed-holder")
                    && reservations.All(reservation => reservation.UserId.Value != "newest-holder");
            }

            public bool IsCompleted(bool state) => state;

            public string Describe(bool state)
                => $"Availability saga and reservation state for product {_productId} were not completed.";
        }

        private sealed class StockSnapshotReadyOperation : IMessageProcessingOperation<bool>
        {
            private readonly MsSqlE2EFixture _fixture;
            private readonly int _productId;

            public StockSnapshotReadyOperation(MsSqlE2EFixture fixture, int productId)
            {
                _fixture = fixture;
                _productId = productId;
            }

            public async Task<bool> ReadAsync(CancellationToken cancellationToken)
            {
                using var scope = _fixture.Services.CreateScope();
                var snapshot = await scope.ServiceProvider
                    .GetRequiredService<IStockSnapshotRepository>()
                    .FindByProductIdAsync(new PresaleProductId(_productId), cancellationToken);
                return snapshot is not null;
            }

            public bool IsCompleted(bool state) => state;

            public string Describe(bool state)
                => $"Stock snapshot for product {_productId} was not created by the initialization outbox event.";
        }
    }
}
