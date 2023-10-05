using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Web;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.IntegrationTests.Common
{
    public class BaseTest<T> : CustomWebApplicationFactory<Startup>, IDisposable where T : class
    {
        protected readonly T _service;

        protected BaseTest()
        {
            _service = Services.GetService(typeof(T)) as T;
        }

        public new virtual void Dispose()
        {
            var context = Services.GetService(typeof(Context)) as Context;
            context.Database.EnsureDeleted();
            context.Dispose();
            base.Dispose();
        }
    }
}
