using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.E2E.Backend.Infrastructure;
using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.E2E.Backend.Persistence.QueryTranslation
{
    /// <summary>
    /// FOR ANYONE ADDING EF QUERIES: real <see cref="IOrderRepository"/> search methods against a real SQL
    /// Server (Testcontainers), on seeded rows, asserting the RESULTS. The order list search filters by a fragment
    /// of <c>Order.Number</c>, a value object mapped with <c>HasConversion</c>. A predicate written as
    /// <c>o.Number.Value.Contains(search)</c> passes on the InMemory provider but cannot be translated to SQL, so it
    /// only fails in production. See <see cref="UserProfileQueryTranslationE2ETests"/> for the full rationale and
    /// how to extend this folder, and <c>.github/skills/linq-complexity-discovery/SKILL.md</c> for the trigger list.
    /// <para>
    /// The database is shared by the whole collection, so every test searches for a fragment of its own unique
    /// order number. Letter-case behaviour of the search is decided by the database collation (the Testcontainers
    /// default is case-insensitive); the case test pins that behaviour for the default collation.
    /// </para>
    /// </summary>
    [Collection(MsSqlE2EFixture.CollectionName)]
    public class OrderSearchQueryTranslationE2ETests : SqlServerE2ETestBase<IOrderRepository>
    {
        private readonly MsSqlE2EFixture _fixture;

        public OrderSearchQueryTranslationE2ETests(MsSqlE2EFixture fixture) : base(fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetAllAsync_SearchByNumberFragment_ReturnsOnlyMatchingOrder()
        {
            var seeded = await SeedOrderAsync(paid: false);
            await SeedOrderAsync(paid: false);

            var page = await Service.GetAllAsync(50, 1, seeded.UniqueFragment, CancellationToken);
            var count = await Service.GetAllCountAsync(seeded.UniqueFragment, CancellationToken);

            page.Select(o => o.Id.Value).ShouldBe(new[] { seeded.Id });
            count.ShouldBe(1);
        }

        [Fact]
        public async Task GetAllAsync_SearchWithDifferentLetterCase_MatchesOnDefaultCollation()
        {
            var seeded = await SeedOrderAsync(paid: false);

            var page = await Service.GetAllAsync(50, 1, seeded.UniqueFragment.ToLowerInvariant(), CancellationToken);

            page.Select(o => o.Id.Value).ShouldBe(new[] { seeded.Id });
        }

        [Fact]
        public async Task GetAllAsync_SearchWithNoMatch_ReturnsEmptyAndZeroCount()
        {
            await SeedOrderAsync(paid: false);

            var page = await Service.GetAllAsync(50, 1, "ZZZZZZZZZZ", CancellationToken);
            var count = await Service.GetAllCountAsync("ZZZZZZZZZZ", CancellationToken);

            page.ShouldBeEmpty();
            count.ShouldBe(0);
        }

        [Theory]
        [InlineData("ORD_")]
        [InlineData("%")]
        public async Task GetAllAsync_SearchWithLikeWildcardCharacters_TreatsThemLiterally(string search)
        {
            await SeedOrderAsync(paid: false);

            var page = await Service.GetAllAsync(50, 1, search, CancellationToken);
            var count = await Service.GetAllCountAsync(search, CancellationToken);

            // An unescaped LIKE would treat '_' as "any single character" and '%' as "anything" and match every
            // order; EF escapes them (ESCAPE N'\'), so no order number contains these literal characters.
            page.ShouldBeEmpty();
            count.ShouldBe(0);
        }

        [Fact]
        public async Task GetAllAsync_EmptySearch_StillTranslatesAndReturnsRows()
        {
            var seeded = await SeedOrderAsync(paid: false);

            var total = await Service.GetAllCountAsync(string.Empty, CancellationToken);
            var page = await Service.GetAllAsync(total, 1, string.Empty, CancellationToken);

            page.Select(o => o.Id.Value).ShouldContain(seeded.Id);
        }

        [Fact]
        public async Task GetAllPaidAsync_SearchByNumberFragment_ReturnsOnlyPaidMatchingOrder()
        {
            var paid = await SeedOrderAsync(paid: true);
            var unpaid = await SeedOrderAsync(paid: false);

            var paidPage = await Service.GetAllPaidAsync(50, 1, paid.UniqueFragment, CancellationToken);
            var paidCount = await Service.GetAllPaidCountAsync(paid.UniqueFragment, CancellationToken);
            var unpaidPage = await Service.GetAllPaidAsync(50, 1, unpaid.UniqueFragment, CancellationToken);
            var unpaidCount = await Service.GetAllPaidCountAsync(unpaid.UniqueFragment, CancellationToken);

            paidPage.Select(o => o.Id.Value).ShouldBe(new[] { paid.Id });
            paidCount.ShouldBe(1);
            unpaidPage.ShouldBeEmpty();
            unpaidCount.ShouldBe(0);
        }

        [Fact]
        public async Task GetAllPaidAsync_EmptySearch_StillTranslatesAndReturnsPaidRows()
        {
            var paid = await SeedOrderAsync(paid: true);

            var total = await Service.GetAllPaidCountAsync(string.Empty, CancellationToken);
            var page = await Service.GetAllPaidAsync(total, 1, string.Empty, CancellationToken);

            page.Select(o => o.Id.Value).ShouldContain(paid.Id);
        }

        private async Task<SeededOrder> SeedOrderAsync(bool paid)
        {
            await _fixture.EnsureTestCustomerAsync();

            // Valid ORD-YYYYMMDD-XXXXXXXX number whose suffix always contains letters ("AB" + 6 random hex), so the
            // letter-case test can never pass trivially on an all-digit fragment. The suffix is the whole 8-hex part
            // of the number, taken from a GUID, with its first character forced to a letter (A-F). The search matches
            // a substring ('%...%') and the tests assert exact counts, so the fragment has to be unique in the shared
            // database; 6 * 16^7 (~1.6 billion) values make a collision negligible.
            var guidBytes = Guid.NewGuid().ToByteArray();
            var uniqueFragment = (char)('A' + guidBytes[0] % 6) + Convert.ToHexString(guidBytes, 1, 4)[..7];

            var number = OrderNumber.Parse($"ORD-{DateTime.UtcNow:yyyyMMdd}-{uniqueFragment}");
            var order = Order.Create(
                1,
                1,
                new OrderUserId(MsSqlE2EFixture.TestCustomerId),
                number,
                new OrderCustomer(
                    "Jan",
                    "Translation",
                    "jan@translation.test",
                    "123456789",
                    false,
                    null,
                    null,
                    "Glowna",
                    "1",
                    null,
                    "67-100",
                    "Nowa Sol",
                    "Polska"));
            var id = await Service.AddAsync(order, CancellationToken);

            if (paid)
            {
                order.ConfirmPayment(1);
                await Service.UpdateAsync(order, CancellationToken);
            }

            // The suffix is a fragment of the full number, so the search exercises partial (LIKE '%...%')
            // matching rather than equality.
            return new SeededOrder(id, uniqueFragment);
        }

        private sealed record SeededOrder(int Id, string UniqueFragment);
    }
}
