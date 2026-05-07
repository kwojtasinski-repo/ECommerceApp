using ECommerceApp.API;
using ECommerceApp.Shared.TestInfrastructure;

namespace ECommerceApp.IntegrationTests.API
{
    /// <summary>
    /// Minimal web application factory used to test HTTP-level validation behavior.
    /// Relies on the standard seed data provided by <see cref="CustomWebApplicationFactory{TStartup}"/>.
    /// </summary>
    public sealed class ValidationApiTestFactory : CustomWebApplicationFactory<Startup>
    {
    }
}

