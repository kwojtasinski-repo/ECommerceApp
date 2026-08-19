using ECommerceApp.Application.Inventory.Availability;
using ECommerceApp.Application.Messaging;
using Moq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.UnitTests.Shared.Setup
{
    public static class InventoryTransactionMockExtensions
    {
        public static Mock<IInventoryUnitOfWork> SetupInventoryTransaction(
            this Mock<IInventoryUnitOfWork> unitOfWork,
            Mock<IOutboxTransaction> transaction)
        {
            transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);
            return unitOfWork;
        }

        public static Mock<IOutboxWriter> SetupSuccessfulOutboxEnqueue(
            this Mock<IOutboxWriter> outboxWriter)
        {
            outboxWriter.Setup(w => w.EnqueueAsync(
                    It.IsAny<IMessage>(),
                    It.IsAny<IOutboxTransaction>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return outboxWriter;
        }
    }
}