using ECommerceApp.Domain.Supporting.Verification;
using ECommerceApp.E2E.Backend.Infrastructure;
using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.E2E.Backend.Supporting.Verification
{
    [Collection("SqlServerE2E")]
    public class VerificationCodeRepositoryE2ETests : SqlServerE2ETestBase<IVerificationCodeRepository>
    {
        public VerificationCodeRepositoryE2ETests(MsSqlE2EFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task VerificationCodeRepository_RoundTripAndPendingFilter_ShouldPersistExpectedRows()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var pending = VerificationCode.Create(
                VerificationPurpose.GuestAccountLink,
                $"pending-subject-{suffix}",
                $"pending-code-{suffix}",
                DateTime.UtcNow.AddMinutes(10));
            var expired = VerificationCode.Create(
                VerificationPurpose.GuestAccountLink,
                $"expired-subject-{suffix}",
                $"expired-code-{suffix}",
                DateTime.UtcNow.AddMinutes(-10));
            var consumed = VerificationCode.Create(
                VerificationPurpose.GuestOrderAccess,
                $"consumed-subject-{suffix}",
                $"consumed-code-{suffix}",
                DateTime.UtcNow.AddMinutes(10));

            await Service.AddAsync(pending, CancellationToken);
            await Service.AddAsync(expired, CancellationToken);
            await Service.AddAsync(consumed, CancellationToken);
            consumed.Consume();
            await Service.UpdateAsync(consumed, CancellationToken);

            var roundTrip = await Service.GetByCodeAsync(
                pending.Code,
                pending.Purpose,
                CancellationToken);
            var pendingCodes = await Service.GetPendingAsync(CancellationToken);

            roundTrip.ShouldNotBeNull();
            roundTrip.SubjectKey.ShouldBe(pending.SubjectKey);
            roundTrip.Code.ShouldBe(pending.Code);
            roundTrip.Purpose.ShouldBe(pending.Purpose);
            pendingCodes.ShouldContain(code => code.Code == pending.Code);
            pendingCodes.ShouldNotContain(code => code.Code == expired.Code);
            pendingCodes.ShouldNotContain(code => code.Code == consumed.Code);
            pendingCodes.Count(code => code.Code == pending.Code).ShouldBe(1);
        }
    }
}