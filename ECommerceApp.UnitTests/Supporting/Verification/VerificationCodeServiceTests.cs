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

        private void SetupCodePersistence()
        {
            _repository
                .Setup(repository => repository.AddAsync(It.IsAny<VerificationCode>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private void SetupCodeLookup(VerificationCode code, VerificationPurpose purpose)
        {
            _repository
                .Setup(repository => repository.GetByCodeAsync("code", purpose, It.IsAny<CancellationToken>()))
                .ReturnsAsync(code);
        }

        private async Task<string[]> GenerateCodes(int count)
        {
            var codes = new string[count];
            for (var index = 0; index < codes.Length; index++)
            {
                codes[index] = await _sut.GenerateAsync(
                    VerificationPurpose.GuestAccountLink,
                    "subject",
                    TimeSpan.FromMinutes(5),
                    TestContext.Current.CancellationToken);
            }

            return codes;
        }

        [Fact]
        public async Task GenerateAsync_ReturnsHighEntropyUniqueCode()
        {
            // Arrange
            SetupCodePersistence();

            // Act
            var codes = await GenerateCodes(10);

            // Assert
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
            // Arrange
            var verificationCode = VerificationCode.Create(
                VerificationPurpose.GuestAccountLink,
                "subject",
                "code",
                DateTime.UtcNow.AddMinutes(5));
            SetupCodeLookup(verificationCode, VerificationPurpose.GuestAccountLink);

            // Act
            var result = await _sut.TryConsumeAsync(
                "code",
                VerificationPurpose.GuestAccountLink,
                TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be("subject");
            verificationCode.ConsumedAt.Should().NotBeNull();
            _repository.Verify(repository => repository.UpdateAsync(
                verificationCode,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TryConsumeAsync_WrongPurpose_ReturnsNullWithoutConsuming()
        {
            // Arrange
            var verificationCode = VerificationCode.Create(
                VerificationPurpose.GuestAccountLink,
                "subject",
                "code",
                DateTime.UtcNow.AddMinutes(5));
            SetupCodeLookup(null, VerificationPurpose.GuestOrderAccess);

            // Act
            var result = await _sut.TryConsumeAsync(
                "code",
                VerificationPurpose.GuestOrderAccess,
                TestContext.Current.CancellationToken);

            // Assert
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
            // Arrange
            var verificationCode = VerificationCode.Create(
                VerificationPurpose.GuestAccountLink,
                "subject",
                "code",
                DateTime.UtcNow.AddMinutes(-1));
            SetupCodeLookup(verificationCode, VerificationPurpose.GuestAccountLink);

            // Act
            var result = await _sut.TryConsumeAsync(
                "code",
                VerificationPurpose.GuestAccountLink,
                TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
            verificationCode.ConsumedAt.Should().BeNull();
            _repository.Verify(repository => repository.UpdateAsync(
                It.IsAny<VerificationCode>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task TryConsumeAsync_AlreadyConsumedCode_ReturnsNullWithoutConsuming()
        {
            // Arrange
            var verificationCode = VerificationCode.Create(
                VerificationPurpose.GuestAccountLink,
                "subject",
                "code",
                DateTime.UtcNow.AddMinutes(5));
            verificationCode.Consume();
            var consumedAt = verificationCode.ConsumedAt;
            SetupCodeLookup(verificationCode, VerificationPurpose.GuestAccountLink);

            // Act
            var result = await _sut.TryConsumeAsync(
                "code",
                VerificationPurpose.GuestAccountLink,
                TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
            verificationCode.ConsumedAt.Should().Be(consumedAt);
            _repository.Verify(repository => repository.UpdateAsync(
                It.IsAny<VerificationCode>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}