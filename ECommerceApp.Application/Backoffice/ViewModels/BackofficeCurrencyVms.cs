using System.Collections.Generic;

namespace ECommerceApp.Application.Backoffice.ViewModels
{
    public sealed class BackofficeCurrencyListVm
    {
        public IReadOnlyList<BackofficeCurrencyItemVm> Currencies { get; init; } = new List<BackofficeCurrencyItemVm>();
    }

    public sealed class BackofficeCurrencyItemVm
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    public sealed class BackofficeCurrencyDetailVm
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public IReadOnlyList<BackofficeCurrencyRateVm> Rates { get; init; } = new List<BackofficeCurrencyRateVm>();
    }

    public sealed class BackofficeCurrencyRateVm
    {
        public int Id { get; init; }
        public decimal Rate { get; init; }
        public string CurrencyDate { get; init; } = string.Empty;
    }
}
