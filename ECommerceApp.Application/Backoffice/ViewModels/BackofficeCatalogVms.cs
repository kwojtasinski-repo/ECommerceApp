using System.Collections.Generic;

namespace ECommerceApp.Application.Backoffice.ViewModels
{
    public sealed class BackofficeCatalogListVm
    {
        public IReadOnlyList<BackofficeCatalogItemVm> Products { get; init; } = new List<BackofficeCatalogItemVm>();
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public string SearchString { get; init; }
    }

    public sealed class BackofficeCatalogItemVm
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public decimal Cost { get; init; }
        public string Status { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
    }

    public sealed class BackofficeCatalogDetailVm
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public decimal Cost { get; init; }
        public string Status { get; init; } = string.Empty;
        public int CategoryId { get; init; }
        public string CategoryName { get; init; } = string.Empty;
    }
}
