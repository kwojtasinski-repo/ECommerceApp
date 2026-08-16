using ECommerceApp.Application.Supporting.Verification.Services;
using ECommerceApp.Domain.Supporting.Verification;
using AwesomeAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.Verification
{
    public class VerificationCodeServiceTests
    {
        private readonly Mock<IVerificationCodeRepository> _repository;
        private readonly VerificationCodeService _sut;

        public VerificationCodeServiceTests()
        {
            _repository = new Mock<IVerificationCodeRepository>();
            _sut = new VerificationCodeService(_repository.Object);
        }

        [Fact]
        public async Task GenerateAsync_ReturnsHighEntropyUniqueCode()
        {
            var codes = new string[10];
            _repository
                .Setup(repository => repository.AddAsync(It.IsAny<VerificationCode>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            for (var index = 0; index < codes.Length; index++)
            {
                codes[index] = await _sut.GenerateAsync(
                    VerificationPurpose.GuestAccountLink,
                    "subject",
                    TimeSpan.FromMinutes(5),
                    TestContext.Current.CancellationToken);
            }

            codes.Should().OnlyHaveUniqueItems();
            codes.Should().AllSatisfy(code =>
            {
                code.Length.Should().Be(64);
                code.Should().MatchRegex("^[0-9a-f]+$");
            });
        }

        [Fact]
        public async Task TryConsumeAsync_ValidCode_ConsumesAndReturnsSubjectKey()
        {
            var verificationCode = VerificationCode.Create(
                VerificationPurpose.GuestAccountLink,
                "subject",
                "code",
                DateTime.UtcNow.AddMinutes(5));
            _repository
                .Setup(repository => repository.GetByCodeAsync(
                    "code",
                    VerificationPurpose.GuestAccountLink,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(verificationCode);

            var result = await _sut.TryConsumeAsync(
                "code",
                VerificationPurpose.GuestAccountLink,
                TestContext.Current.CancellationToken);

            result.Should().Be("subject");
            verificationCode.ConsumedAt.Should().NotBeNull();
            _repository.Verify(repository => repository.UpdateAsync(
                verificationCode,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TryConsumeAsync_WrongPurpose_ReturnsNullWithoutConsuming()
        {
            var verificationCode = VerificationCode.Create(
                VerificationPurpose.GuestAccountLink,
                "subject",
                "code",
                DateTime.UtcNow.AddMinutes(5));
            _repository
                .Setup(repository => repository.GetByCodeAsync(
                    "code",
                    VerificationPurpose.GuestOrderAccess,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((VerificationCode)null);

            var result = await _sut.TryConsumeAsync(
                "code",
                VerificationPurpose.GuestOrderAccess,
                TestContext.Current.CancellationToken);

            result.Should().BeNull();
            verificationCode.ConsumedAt.Should().BeNull();
            _repository.Verify(repository => repository.GetByCodeAsync(
                "code",
                VerificationPurpose.GuestOrderAccess,
                It.IsAny<CancellationToken>()), Times.Once);
            _repository.Verify(repository => repository.UpdateAsync(
                It.IsAny<VerificationCode>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task TryConsumeAsync_ExpiredCode_ReturnsNullWithoutConsuming()
        {
            var verificationCode = VerificationCode.Create(
                VerificationPurpose.GuestAccountLink,
                "subject",
                "code",
                DateTime.UtcNow.AddMinutes(-1));
            _repository
                .Setup(repository => repository.GetByCodeAsync(
                    "code",
                    VerificationPurpose.GuestAccountLink,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(verificationCode);

            var result = await _sut.TryConsumeAsync(
                "code",
                VerificationPurpose.GuestAccountLink,
                TestContext.Current.CancellationToken);

            result.Should().BeNull();
            verificationCode.ConsumedAt.Should().BeNull();
            _repository.Verify(repository => repository.UpdateAsync(
                It.IsAny<VerificationCode>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task TryConsumeAsync_AlreadyConsumedCode_ReturnsNullWithoutConsuming()
        {
            var verificationCode = VerificationCode.Create(
                VerificationPurpose.GuestAccountLink,
                "subject",
                "code",
                DateTime.UtcNow.AddMinutes(5));
            verificationCode.Consume();
            var consumedAt = verificationCode.ConsumedAt;
            _repository
                .Setup(repository => repository.GetByCodeAsync(
                    "code",
                    VerificationPurpose.GuestAccountLink,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(verificationCode);

            var result = await _sut.TryConsumeAsync(
                "code",
                VerificationPurpose.GuestAccountLink,
                TestContext.Current.CancellationToken);

            result.Should().BeNull();
            verificationCode.ConsumedAt.Should().Be(consumedAt);
            _repository.Verify(repository => repository.UpdateAsync(
                It.IsAny<VerificationCode>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}