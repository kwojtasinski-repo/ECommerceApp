using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ECommerceApp.Shared.TestInfrastructure
{
    /// <summary>
    /// Base class for API HTTP integration tests that use <see cref="IClassFixture{TFactory}"/>.
    /// Automatically routes application <see cref="Microsoft.Extensions.Logging.ILogger"/> calls
    /// to the xUnit per-test output via <see cref="IHaveXunitSink.Sink"/>.
    ///
    /// <para>Usage:</para>
    /// <code>
    /// public class MyControllerTests : ApiTestBase&lt;MyFactory&gt;, IClassFixture&lt;MyFactory&gt;
    /// {
    ///     public MyControllerTests(MyFactory factory, ITestOutputHelper output)
    ///         : base(factory, output) { }
    /// }
    /// </code>
    /// </summary>
    public abstract class ApiTestBase<TFactory> : IAsyncLifetime
        where TFactory : class, IHaveXunitSink
    {
        protected readonly TFactory _factory;
        protected readonly ITestOutputHelper _output;

        protected ApiTestBase(TFactory factory, ITestOutputHelper output)
        {
            _factory = factory;
            _output = output;
        }

        public virtual Task InitializeAsync()
        {
            _factory.Sink.SetOutput(_output);
            return Task.CompletedTask;
        }

        public virtual Task DisposeAsync()
        {
            _factory.Sink.SetOutput(null);
            return Task.CompletedTask;
        }
    }
}

