using System.Text.RegularExpressions;

namespace RagTools.Core.Primitives;

/// <summary>
/// A validated Qdrant collection name. Construct via <see cref="Parse"/> or <see cref="TryParse"/>.
/// Once constructed, the value is guaranteed to match <c>^[a-z0-9][a-z0-9_-]*$</c> and be 1..64 chars.
/// </summary>
public readonly record struct CollectionName
{
    private const int MaxLength = 64;

    private static readonly Regex Pattern =
        new(@"^[a-z0-9][a-z0-9_-]*$", RegexOptions.Compiled);

    public string Value { get; }

    private CollectionName(string value) => Value = value;

    public static CollectionName Parse(string? raw) =>
        TryParse(raw, out var result)
            ? result
            : throw new ArgumentException($"Invalid collection name: '{raw}'", nameof(raw));

    public static bool TryParse(string? raw, out CollectionName result)
    {
        result = default;
        if (string.IsNullOrEmpty(raw))  
        {
            return false;
        }

        if (raw.Length > MaxLength)     
        {
            return false;
        }

        if (!Pattern.IsMatch(raw))      
        {
            return false;
        }

        result = new CollectionName(raw);
        return true;
    }

    public override string ToString() => Value;

    public static implicit operator string(CollectionName c) => c.Value;
}
