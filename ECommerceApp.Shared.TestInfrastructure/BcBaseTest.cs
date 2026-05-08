using ECommerceApp.Application.Messaging;
using ECommerceApp.Infrastructure.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using Xunit;

namespace ECommerceApp.Shared.TestInfrastructure
{
    /// <summary>
    /// Base class for new BC integration tests. Uses <see cref="BcWebApplicationFactory"/>
    /// which swaps ALL per-BC DbContexts to InMemory and uses a synchronous multi-handler
    /// <see cref="IMessageBroker"/>.
    ///
    /// <para>Use for:</para>
    /// <list type="bullet">
    ///   <item>Per-BC service tests (e.g. <c>BcBaseTest&lt;IPaymentService&gt;</c>)</item>
    ///   <item>Cross-BC event chain tests (resolve <see cref="IMessageBroker"/> and publish events)</item>
    /// </list>
    /// </summary>
    public class BcBaseTest<T> : BcWebApplicationFactory, IDisposable where T : class
    {
        protected readonly string PROPER_CUSTOMER_ID = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";
        protected readonly T _service;

        protected BcBaseTest(ITestOutputHelper output)
        {
            Sink.SetOutput(output);
            _service = Services.GetRequiredService<T>();
        }

        public new virtual void Dispose()
        {
            var context = Services.GetService(typeof(Context)) as Context;
            if (context != null)
            {
                context.Database.EnsureDeleted();
                context.Dispose();
            }
            Sink.SetOutput(null);
            base.Dispose();
        }

        /// <summary>
        /// Resolves any service from the DI container. Use for cross-BC assertions
        /// (e.g. resolve <see cref="IMessageBroker"/> or a secondary BC service).
        /// </summary>
        protected TService GetRequiredService<TService>() where TService : notnull
        {
            return Services.GetRequiredService<TService>();
        }

        /// <summary>
        /// CancellationToken tied to the current xUnit v3 test run.
        /// Cancelled automatically when the test is stopped/aborted.
        /// </summary>
        protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

        /// <summary>
        /// Publishes a message through the synchronous multi-handler broker.
        /// All registered handlers fire synchronously — assertions are safe immediately after.
        /// </summary>
        protected async System.Threading.Tasks.Task PublishAsync(IMessage[] messages, CancellationToken cancellationToken = default)
        {
            var broker = GetRequiredService<IMessageBroker>();
            await broker.PublishAsync(messages);
        }

        /// <inheritdoc cref="PublishAsync(IMessage[], CancellationToken)"/>
        protected System.Threading.Tasks.Task PublishAsync(IMessage message, CancellationToken cancellationToken = default)
            => PublishAsync([message], cancellationToken);

        protected void SetHttpContextUserId(string userId)
        {
            var httpContextAccessor = Services.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
            if (httpContextAccessor?.HttpContext == null) return;

            var claims = GetUserClaims(httpContextAccessor.HttpContext.User);
            var userIdClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim != null)
                claims.Remove(userIdClaim);

            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            httpContextAccessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        }

        protected void SetUserRole(string role)
        {
            var httpContextAccessor = Services.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
            if (httpContextAccessor?.HttpContext == null) return;

            var claims = GetUserClaims(httpContextAccessor.HttpContext.User);
            var roleClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            if (roleClaim != null)
                claims.Remove(roleClaim);

            claims.Add(new Claim(ClaimTypes.Role, role));
            httpContextAccessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        }

        private static List<Claim> GetUserClaims(ClaimsPrincipal user)
        {
            return user.Claims.Select(c => new Claim(c.Type, c.Value)).ToList();
        }

        protected override void OverrideServicesImplementation(IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessorTest>();
        }
    }
}

