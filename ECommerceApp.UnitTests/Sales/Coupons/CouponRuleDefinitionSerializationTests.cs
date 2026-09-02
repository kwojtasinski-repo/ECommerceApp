using System.Collections.Generic;
using System.Text.Json;
using AwesomeAssertions;
using ECommerceApp.Domain.Sales.Coupons;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    /// <summary>
    /// The rules JSON is written by one place and read by two (the aggregate and CouponService).
    /// These tests pin the wire format so those readers cannot drift apart again — a mismatch here
    /// previously made every string-category coupon fail to create while the suite stayed green.
    /// </summary>
    public class CouponRuleDefinitionSerializationTests
    {
        // Verbatim the placeholder shown to the admin in Areas/Sales/Views/Coupon/Create.cshtml.
        private const string UiJson =
            "[{\"Name\":\"percentage-off\",\"Category\":\"Discount\",\"Parameters\":{\"percent\":\"10\"}}]";

        [Fact]
        public void ParseList_ShouldAcceptStringCategory()
        {
            var rules = CouponRuleDefinition.ParseList(UiJson);

            rules.Should().HaveCount(1);
            rules[0].Name.Should().Be("percentage-off");
            rules[0].Category.Should().Be(CouponRuleCategory.Discount);
            rules[0].Parameters["percent"].Should().Be("10");
        }

        [Fact]
        public void ParseList_ShouldStillAcceptNumericCategory_ForAlreadyPersistedJson()
        {
            var numericJson = "[{\"Name\":\"percentage-off\",\"Category\":1,\"Parameters\":{}}]";

            var rules = CouponRuleDefinition.ParseList(numericJson);

            rules[0].Category.Should().Be(CouponRuleCategory.Discount);
        }

        [Fact]
        public void SerializeList_ShouldWriteCategoryAsString()
        {
            var json = CouponRuleDefinition.SerializeList(new[]
            {
                new CouponRuleDefinition("percentage-off", CouponRuleCategory.Discount,
                    new Dictionary<string, string> { ["percent"] = "10" })
            });

            json.Should().Contain("\"Category\":\"Discount\"");
        }

        [Fact]
        public void SerializeList_AndParseList_ShouldRoundTrip()
        {
            var original = new[]
            {
                new CouponRuleDefinition("order-total", CouponRuleCategory.Scope, new Dictionary<string, string>()),
                new CouponRuleDefinition("min-order-value", CouponRuleCategory.Constraint,
                    new Dictionary<string, string> { ["minValue"] = "99.5" })
            };

            var parsed = CouponRuleDefinition.ParseList(CouponRuleDefinition.SerializeList(original));

            parsed.Should().HaveCount(2);
            parsed[0].Category.Should().Be(CouponRuleCategory.Scope);
            parsed[1].Category.Should().Be(CouponRuleCategory.Constraint);
            parsed[1].Parameters["minValue"].Should().Be("99.5");
        }

        [Fact]
        public void ParseList_ShouldThrowJsonException_OnMalformedInput()
        {
            var act = () => CouponRuleDefinition.ParseList("{ not json ]");

            act.Should().Throw<JsonException>();
        }
    }
}
