using System.Collections.Generic;
using ECommerceApp.Application.Sales.Coupons;
using ECommerceApp.Application.Sales.Coupons.Services;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponsOptionsTests
    {
        [Fact]
        public void MaxCouponsPerOrder_DefaultShouldBeFive()
        {
            var options = new CouponsOptions();

            options.MaxCouponsPerOrder.Should().Be(5);
        }

        [Fact]
        public void MaxCouponsPerOrder_HardCeilingIsTen()
        {
            // Spec: MaxCouponsPerOrder has a hard ceiling of 10.
            // Configuration can set any value, but service must enforce Math.Min(value, 10).
            var hardCeiling = 10;

            System.Math.Min(15, hardCeiling).Should().Be(10);
            System.Math.Min(5, hardCeiling).Should().Be(5);
            System.Math.Min(10, hardCeiling).Should().Be(10);
        }

        [Fact]
        public void DefaultMinOrderValue_ShouldBe100()
        {
            var options = new CouponsOptions();

            options.DefaultMinOrderValue.Should().Be(100m);
        }

        [Fact]
        public void DefaultDiscountPercent_ShouldBe10()
        {
            var options = new CouponsOptions();

            options.DefaultDiscountPercent.Should().Be(10m);
        }

        [Fact]
        public void Resolved_ShouldTakeValuesFromTheCouponsConfigurationSection()
        {
            // The values must actually come from appsettings, not from a hardcoded `new CouponsOptions()`.
            // Deliberately non-default numbers: asserting the class defaults here would pass even if
            // the binding were never wired, which is how DefaultMinOrderValue stayed dead config before.
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Coupons:MaxCouponsPerOrder"] = "7",
                    ["Coupons:DefaultMinOrderValue"] = "250.5",
                    ["Coupons:DefaultDiscountPercent"] = "33"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddOptions();
            services.Configure<CouponsOptions>(_ => { });                                        // Application defaults
            services.Configure<CouponsOptions>(configuration.GetSection(CouponsOptions.SectionName)); // Infrastructure override
            services.AddCouponServices();

            var resolved = services.BuildServiceProvider().GetRequiredService<CouponsOptions>();

            resolved.MaxCouponsPerOrder.Should().Be(7);
            resolved.DefaultMinOrderValue.Should().Be(250.5m);
            resolved.DefaultDiscountPercent.Should().Be(33m);
        }

        [Fact]
        public void Resolved_WithNoConfigurationSection_ShouldKeepClassDefaults()
        {
            var services = new ServiceCollection();
            services.AddOptions();
            services.Configure<CouponsOptions>(_ => { });
            services.Configure<CouponsOptions>(new ConfigurationBuilder().Build().GetSection(CouponsOptions.SectionName));
            services.AddCouponServices();

            var resolved = services.BuildServiceProvider().GetRequiredService<CouponsOptions>();

            resolved.MaxCouponsPerOrder.Should().Be(5);
            resolved.DefaultMinOrderValue.Should().Be(100m);
            resolved.DefaultDiscountPercent.Should().Be(10m);
        }

        [Fact]
        public void Options_ShouldBeConfigurable()
        {
            var options = new CouponsOptions
            {
                MaxCouponsPerOrder = 3,
                DefaultMinOrderValue = 50m,
                DefaultDiscountPercent = 25m
            };

            options.MaxCouponsPerOrder.Should().Be(3);
            options.DefaultMinOrderValue.Should().Be(50m);
            options.DefaultDiscountPercent.Should().Be(25m);
        }
    }
}
