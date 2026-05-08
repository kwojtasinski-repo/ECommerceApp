using System.Threading;
using System.Threading.Tasks;
using Xunit;

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

        /// <summary>
        /// CancellationToken tied to the current xUnit v3 test run.
        /// Cancelled automatically when the test is stopped/aborted.
        /// </summary>
        protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

        public virtual ValueTask InitializeAsync()
        {
            _factory.Sink.SetOutput(_output);
            return ValueTask.CompletedTask;
        }

        public virtual ValueTask DisposeAsync()
        {
            _factory.Sink.SetOutput(null);
            return ValueTask.CompletedTask;
        }
    }
}

