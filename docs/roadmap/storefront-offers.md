# Roadmap: Storefront — `/offers` Public Browsing

> Status: ✅ §1 routing + §2 home page done — §3 category strip optional
> Scope: `Web` project — `Presale` area + `Home` controller

---

## Goal

Replace the legacy `Item` controller flow with a clean public storefront using
`IStorefrontQueryService` (Presale BC). Public URLs use `/offers` (English convention).

---

## What already exists ✅

| Layer | Status |
|---|---|
| `IStorefrontQueryService` — `GetPublishedProductsAsync` | ✅ |
| `IStorefrontQueryService` — `GetPublishedProductsByTagAsync` | ✅ |
| `IStorefrontQueryService` — `GetProductDetailsAsync` (stock-joined) | ✅ |
| `ICatalogClient` + `CatalogClientAdapter` for all three methods | ✅ |
| `Presale/Storefront/StorefrontController` — `Index`, `ByTag`, `Details` | ✅ |
| `Presale/Storefront/Index.cshtml` — grid + tag pills + stock badge | ✅ |
| `Presale/Storefront/ByTag.cshtml` — filtered grid + pagination | ✅ |
| `Presale/Storefront/Details.cshtml` — details + stock quantity + tag links | ✅ |
| `Home/Index.cshtml` — "Pokaż przedmioty" → Storefront `Index` | ✅ |

---

## What still needs to be built

### 1 — URL routing `/offers` (XS)

Change public-facing URLs from the MVC area default (`/Presale/Storefront/...`)
to clean `/offers/...` routes.

| Action | Current URL | Target URL |
|---|---|---|
| `Index` GET | `/Presale/Storefront/Index` | `/offers` |
| `Index` POST (search/page) | `/Presale/Storefront/Index` | `/offers` |
| `ByTag` | `/Presale/Storefront/ByTag?tagId=1` | `/offers/tag/{tagId}` |
| `Details` | `/Presale/Storefront/Details?id=1` | `/offers/{id}` |

**Implementation**: add explicit `[Route]` attributes on `StorefrontController`.
No new files — single controller change.

### 2 — Featured products on Home page (S)

Allegro-style home page: hero + search bar + featured carousel (10 newest published products).

**New service method needed:**

```
IStorefrontQueryService.GetFeaturedProductsAsync(int count, CancellationToken)
  → ICatalogClient.GetFeaturedProductsAsync(int count, CancellationToken)
  → IProductService.GetNewestPublishedProductsAsync(int count)
  → IProductRepository.GetNewestPublishedAsync(int count)
```

Returns `IReadOnlyList<StorefrontProductVm>` — same lean VM, stock-joined.

**`HomeController` changes:**
- Inject `IStorefrontQueryService`
- `Index` becomes `async`, passes featured products + search redirect to view

**`Home/Index.cshtml` changes:**
- Search bar (`GET /offers?searchString=`) — replaces current static button
- Featured product carousel (Bootstrap carousel or simple card row)
- Category strip (optional — see §3)

### 3 — Category strip on Home / browse page (optional, S)

Categories already exist in the Catalog BC (`ICategoryService.GetAllCategories`).
A horizontal scrollable category strip on the home page links to `/offers?categoryId=N`.

Requires:
- `IStorefrontQueryService.GetPublishedProductsByCategoryAsync` (same pattern as ByTag)
- `/offers/category/{categoryId}` route
- `StorefrontController.ByCategory` action
- `Presale/Storefront/ByCategory.cshtml` view

**Decision**: implement after `§1` + `§2` are done. Complexity is the same as ByTag was.

---

## Implementation order

```
§1 URL routing   →   §2 Home page carousel   →   §3 Category strip (optional)
 XS (1 file)          S (4–5 files)                S (5 files)
```

---

## Open questions

- [ ] Search from Home: `GET /offers?searchString=x` (shareable URL) or current `POST`? Recommendation: switch Index to GET with query string — simpler, browser back button works.
- [ ] Category strip on Home page: yes/no for v1?
- [ ] "10 newest published" for featured — or another ranking (most ordered, manual curation)?
