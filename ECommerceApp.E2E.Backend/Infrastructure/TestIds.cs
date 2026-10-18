using System.Threading;

namespace ECommerceApp.E2E.Backend.Infrastructure
{
    /// <summary>
    /// One source of integer ids (product, order, ...) for tests that share a SQL database. Every call returns
    /// a value never returned before in this test run, so two tests cannot collide on an id.
    /// </summary>
    public static class TestIds
    {
        private static int _counter;

        /// <param name="min">Lower bound of the range; keeps test ids away from ids seeded by the application.</param>
        public static int Next(int min = 1_000_000) => min + Interlocked.Increment(ref _counter);
    }
}
