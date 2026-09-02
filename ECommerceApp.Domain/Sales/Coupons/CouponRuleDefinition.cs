using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public enum CouponRuleCategory { Scope, Discount, Constraint }

    public sealed record CouponRuleDefinition(
        string Name,
        CouponRuleCategory Category,
        IReadOnlyDictionary<string, string> Parameters)
    {
        // The wire format is owned by the type that defines the shape, so every reader and writer
        // agrees on it. Category is written as a string ("Discount"); the converter also reads the
        // numeric form, so JSON persisted before this was centralised still parses.
        private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        /// <summary>Parses the canonical rules JSON. Throws <see cref="JsonException"/> on malformed input.</summary>
        public static List<CouponRuleDefinition> ParseList(string json)
        {
            return JsonSerializer.Deserialize<List<CouponRuleDefinition>>(json, SerializerOptions)
                   ?? new List<CouponRuleDefinition>();
        }

        /// <summary>Writes the canonical rules JSON that <see cref="ParseList"/> round-trips.</summary>
        public static string SerializeList(IEnumerable<CouponRuleDefinition> rules)
        {
            return JsonSerializer.Serialize(rules, SerializerOptions);
        }
    }
}
