using ECommerceApp.Web;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.IntegrationTests.Common
{
    public class BaseTest<T> : CustomWebApplicationFactory<Startup> where T : class
    {
        protected readonly T _service;

        protected BaseTest()
        {
            _service = Services.GetService(typeof(T)) as T;
        }
    }
}
