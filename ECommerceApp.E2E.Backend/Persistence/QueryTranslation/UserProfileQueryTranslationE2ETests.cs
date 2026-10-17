using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.E2E.Backend.Infrastructure;
using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.E2E.Backend.Persistence.QueryTranslation
{
    /// <summary>
    /// FOR ANYONE ADDING EF QUERIES: this class (and <see cref="OrderSearchQueryTranslationE2ETests"/>)
    /// runs real repository methods against a real SQL Server (Testcontainers, see
    /// <see cref="MsSqlE2EFixture"/>) on seeded rows and asserts the RESULTS, not just that the query compiles.
    /// <para>
    /// Why it exists: the InMemory provider used by <c>ECommerceApp.UnitTests</c> / <c>ECommerceApp.IntegrationTests</c>
    /// evaluates LINQ in memory, so a predicate such as <c>p.Email.Value == email</c> passes there but cannot be
    /// translated to SQL for a property mapped with <c>HasConversion</c> (EF sees one scalar column, so <c>.Value</c>
    /// has nothing to translate to). The failure only appears on SQL Server, i.e. in production.
    /// </para>
    /// <para>
    /// How to extend: when you add or change a repository query that touches a value object / TypedId mapped with
    /// <c>HasConversion</c>, or any query the <c>linq-complexity-discovery</c> skill flags, add a test here that seeds
    /// rows through the repository and calls the real method. Use unique identifiers (the database is shared by the
    /// whole collection). Skill and trigger list: <c>.github/skills/linq-complexity-discovery/SKILL.md</c>.
    /// </para>
    /// </summary>
    [Collection(MsSqlE2EFixture.CollectionName)]
    public class UserProfileQueryTranslationE2ETests : SqlServerE2ETestBase<IUserProfileRepository>
    {
        public UserProfileQueryTranslationE2ETests(MsSqlE2EFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task GetByEmailAsync_ExistingEmail_ReturnsOnlyThatProfile()
        {
            var email = $"{Guid.NewGuid():N}@translation.test";
            var otherEmail = $"{Guid.NewGuid():N}@translation.test";
            var profileId = await SeedProfileAsync(email);
            await SeedProfileAsync(otherEmail);

            var found = await Service.GetByEmailAsync(email);

            found.Count.ShouldBe(1);
            found[0].Id.Value.ShouldBe(profileId);
            found[0].Email.Value.ShouldBe(email);
        }

        [Fact]
        public async Task GetByEmailAsync_DifferentLetterCase_MatchesBecauseEmailIsNormalized()
        {
            var email = $"{Guid.NewGuid():N}@translation.test";
            var profileId = await SeedProfileAsync(email);

            var found = await Service.GetByEmailAsync(email.ToUpperInvariant());

            found.Select(p => p.Id.Value).ShouldContain(profileId);
        }

        [Fact]
        public async Task GetByEmailAsync_UnknownEmail_ReturnsEmptyList()
        {
            await SeedProfileAsync($"{Guid.NewGuid():N}@translation.test");

            var found = await Service.GetByEmailAsync($"{Guid.NewGuid():N}@nobody.test");

            found.ShouldBeEmpty();
        }

        [Theory]
        [InlineData("not-an-email")]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetByEmailAsync_InvalidEmailFormat_ReturnsEmptyListInsteadOfThrowing(string invalidEmail)
        {
            var found = await Service.GetByEmailAsync(invalidEmail);

            found.ShouldBeEmpty();
        }

        private async Task<int> SeedProfileAsync(string email)
        {
            var profile = UserProfile.Create(
                userId: Guid.NewGuid().ToString(),
                firstName: "Jan",
                lastName: "Translation",
                isCompany: false,
                nip: null,
                companyName: null,
                email: email,
                phoneNumber: "123456789");
            var id = await Service.AddAsync(profile);
            return id.Value;
        }
    }
}
