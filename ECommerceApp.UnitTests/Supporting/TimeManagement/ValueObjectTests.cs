using ECommerceApp.Domain.Shared;
using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using FluentAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.TimeManagement
{
    public class ValueObjectTests
    {
        [Fact]
        public void JobName_ValidValue_ShouldCreate()
        {
            var name = new JobName("CurrencyRateSync");

            name.Value.Should().Be("CurrencyRateSync");
        }

        [Fact]
        public void JobName_TrimsWhitespace()
        {
            var name = new JobName("  MyJob  ");

            name.Value.Should().Be("MyJob");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void JobName_EmptyOrNull_ShouldThrowDomainException(string value)
        {
            Action action = () => new JobName(value);

            action.Should().ThrowExactly<DomainException>().WithMessage("Job name is required.");
        }

        [Fact]
        public void JobName_ExceedsMaxLength_ShouldThrowDomainException()
        {
            var longName = new string('x', 101);

            Action action = () => new JobName(longName);

            action.Should().ThrowExactly<DomainException>().WithMessage("Job name must not exceed 100 characters.");
        }

        [Fact]
        public void JobName_ExactlyMaxLength_ShouldCreate()
        {
            var maxName = new string('x', 100);

            var name = new JobName(maxName);

            name.Value.Should().HaveLength(100);
        }

        [Fact]
        public void CronSchedule_ValidExpression_ShouldCreate()
        {
            var cron = new CronSchedule("15 12 * * *");

            cron.Value.Should().Be("15 12 * * *");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CronSchedule_EmptyOrNull_ShouldThrowDomainException(string value)
        {
            Action action = () => new CronSchedule(value);

            action.Should().ThrowExactly<DomainException>().WithMessage("Cron expression is required.");
        }

        [Theory]
        [InlineData("not-a-cron")]
        [InlineData("99 * * * *")]
        [InlineData("* * * * * * *")]
        public void CronSchedule_InvalidExpression_ShouldThrowDomainException(string value)
        {
            Action action = () => new CronSchedule(value);

            action.Should().ThrowExactly<DomainException>();
        }

        [Fact]
        public void CronSchedule_TrimsWhitespace()
        {
            var cron = new CronSchedule("  */5 * * * *  ");

            cron.Value.Should().Be("*/5 * * * *");
        }

        [Fact]
        public void JobName_ToString_ReturnsValue()
        {
            var name = new JobName("TestJob");

            name.ToString().Should().Be("TestJob");
        }
    }
}
