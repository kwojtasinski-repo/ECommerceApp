using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ECommerceApp.IntegrationTests.CrossBC
{
    /// <summary>
    /// Smoke tests to verify the <see cref="BcBaseTest{T}"/> infrastructure works:
    /// <list type="bullet">
    ///   <item>Per-BC service resolves from DI</item>
    ///   <item><see cref="IMessageBroker"/> is the synchronous multi-handler variant</item>
    ///   <item>InMemory databases accept reads (empty tables → null/empty results)</item>
    /// </list>
    /// </summary>
    public class BcInfrastructureSmokeTests : BcBaseTest<IOrderService>
    {
        public BcInfrastructureSmokeTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void Service_ShouldResolveFromDI()
        {
            _service.ShouldNotBeNull();
        }

        [Fact]
        public void MessageBroker_ShouldBeSynchronousMultiHandler()
        {
            var broker = GetRequiredService<IMessageBroker>();

            broker.ShouldNotBeNull();
            broker.ShouldBeOfType<SynchronousMultiHandlerBroker>();
        }

        [Fact]
        public async Task OrderService_EmptyDb_GetAllShouldReturnEmptyPage()
        {
            var result = await _service.GetAllOrdersAsync(pageSize: 10, pageNo: 1, search: null);

            result.ShouldNotBeNull();
            result.Orders.ShouldNotBeNull();
            result.Orders.ShouldBeEmpty();
        }
    }
}

