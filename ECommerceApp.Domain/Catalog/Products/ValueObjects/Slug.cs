using ECommerceApp.Domain.Shared;
using System.Text.RegularExpressions;

namespace ECommerceApp.Domain.Catalog.Products.ValueObjects
{
    public sealed record Slug
    {
        private static readonly Regex AllowedPattern = new Regex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

        public string Value { get; }

        public Slug(string value)
        {
            var trimmed = value?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Slug is required.");
            if (trimmed.Length > 200)
                throw new DomainException("Slug must not exceed 200 characters.");
            if (!AllowedPattern.IsMatch(trimmed))
                throw new DomainException("Slug must contain only lowercase letters, digits, and hyphens.");
            Value = trimmed;
        }

        public static Slug FromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Cannot create slug from empty name.");

            var slug = name.Trim().ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            if (string.IsNullOrEmpty(slug))
                throw new DomainException("Cannot create a valid slug from the given name.");

            return new Slug(slug);
        }

        public override string ToString() => Value;
    }
}
