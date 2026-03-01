namespace ECommerceApp.Application.AccountProfile.ViewModels
{
    public class AddressFormVm
    {
        public string Street { get; set; }
        public string BuildingNumber { get; set; }
        public int? FlatNumber { get; set; }
        public string ZipCode { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
    }
}
