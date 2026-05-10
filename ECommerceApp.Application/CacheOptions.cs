using System;

namespace ECommerceApp.Application.Constants;

/// <summary>
/// Configures in-memory cache TTLs for all service-layer caches.
/// Bind from appsettings.json under the "Cache" section. Defaults apply when the section is absent.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    // ── Catalog ───────────────────────────────────────────────────────────────
    /// <summary>TTL for paginated published-product lists (ProductService).</summary>
    public TimeSpan CatalogListTtl { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>TTL for individual product detail (ProductService).</summary>
    public TimeSpan CatalogProductTtl { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>TTL for the full category navigation tree (CachedCatalogNavigationService).</summary>
    public TimeSpan CatalogNavigationTtl { get; init; } = TimeSpan.FromMinutes(15);

    // ── Presale ───────────────────────────────────────────────────────────────
    /// <summary>TTL for storefront product details shown on the product page (StorefrontQueryService).</summary>
    public TimeSpan ProductDetailsTtl { get; init; } = TimeSpan.FromMinutes(5);

    // ── Currencies ────────────────────────────────────────────────────────────
    /// <summary>TTL for today's exchange rate (CurrencyRateService).</summary>
    public TimeSpan CurrencyRateLatestTtl { get; init; } = TimeSpan.FromMinutes(60);

    /// <summary>TTL for immutable historical exchange rates (CurrencyRateService).</summary>
    public TimeSpan CurrencyRateHistoricalTtl { get; init; } = TimeSpan.FromHours(24);
}
