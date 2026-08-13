using ECommerceApp.Web.E2E.Infrastructure;
using Xunit;

// One Chromium process for the whole assembly. This deliberately is not an ICollectionFixture: a
// collection is xunit's unit of parallelism, so sharing the browser that way would force every test
// class that needs it into one collection and serialize the suite. An assembly fixture is injected
// into any test class constructor while leaving each class its own collection.
[assembly: AssemblyFixture(typeof(PlaywrightBrowserFixture))]
