namespace ECommerceApp.Application.Supporting.Currencies.ViewModels
{
    public class CurrencyVm
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }

        public static CurrencyVm FromDomain(Domain.Supporting.Currencies.Currency s) => new()
        {
            Id = s.Id?.Value ?? 0,
            Code = s.Code.Value,
            Description = s.Description.Value
        };
    }
}
