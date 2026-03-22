namespace ECommerceApp.Application.Presale.Checkout.ViewModels
{
    public class PlaceOrderVm
    {
        public int CustomerId { get; set; }
        public int CurrencyId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsCompany { get; set; }
        public string? CompanyName { get; set; }
        public string? Nip { get; set; }
        public string Street { get; set; } = string.Empty;
        public string BuildingNumber { get; set; } = string.Empty;
        public string? FlatNumber { get; set; }
        public string ZipCode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }
}
