using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Orders.DTOs
{
    public sealed record PlaceOrderFromPresaleDto(
        int CustomerId,
        int CurrencyId,
        string UserId,
        OrderCustomerData Customer,
        IReadOnlyList<PlaceOrderLineDto> Lines);

    public sealed record OrderCustomerData(
        string FirstName,
        string LastName,
        string Email,
        string PhoneNumber,
        bool IsCompany,
        string CompanyName,
        string Nip,
        string Street,
        string BuildingNumber,
        string FlatNumber,
        string ZipCode,
        string City,
        string Country);
}
