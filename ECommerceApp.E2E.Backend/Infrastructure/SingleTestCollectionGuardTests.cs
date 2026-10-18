using System;
using System.Linq;
using System.Reflection;
using ECommerceApp.E2E.Backend.Presale;
using ECommerceApp.E2E.Backend.Sales.Orders;
using Shouldly;
using Xunit;

namespace ECommerceApp.E2E.Backend.Infrastructure
{
    /// <summary>
    /// The collections below start one container and seed a fixed state once for all their tests. Today each holds
    /// exactly one test, so nothing can depend on another. This guard fails the moment a second test is added, so
    /// the author makes the new test independent (own data, own cleanup) and only then raises the limit.
    /// </summary>
    public class SingleTestCollectionGuardTests
    {
        [Theory]
        [InlineData(CartRecoveryE2EFixture.CollectionName)]
        [InlineData(OrderRollbackE2EFixture.CollectionName)]
        public void Collection_WithItsOwnContainer_HasExactlyOneTest(string collectionName)
        {
            var tests = typeof(SingleTestCollectionGuardTests).Assembly.GetTypes()
                .Where(t => t.GetCustomAttributes<CollectionAttribute>().Any(a => a.Name == collectionName))
                .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public))
                .Where(m => m.IsDefined(typeof(FactAttribute), inherit: true))
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
                .ToList();

            tests.Count.ShouldBe(
                1,
                $"collection '{collectionName}' shares one container and seeded state; a second test would depend on run order. " +
                $"Make the new test independent (unique data, own cleanup), then raise this limit. Found: {string.Join(", ", tests)}");
        }
    }
}
