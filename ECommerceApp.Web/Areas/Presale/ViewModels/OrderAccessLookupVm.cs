namespace ECommerceApp.Web.Areas.Presale.ViewModels
{
    public sealed class OrderAccessLookupVm
    {
        public int OrderId { get; init; }
        public string Email { get; set; }
        public string Code { get; set; }
        public string Message { get; init; }
    }
}