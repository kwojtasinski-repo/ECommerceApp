using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Contracts
{
    public interface IOrderClient
    {
        Task<OrderPlacementResult> PlaceOrderAsync(
            int customerId,
            int currencyId,
            string userId,
            CheckoutCustomer customer,
            IReadOnlyList<CheckoutLine> lines,
            CancellationToken ct = default);
    }

    public sealed record OrderPlacementResult(bool IsSuccess, int? OrderId, string FailureReason)
    {
        public static OrderPlacementResult Succeeded(int orderId) => new(true, orderId, null);
        public static OrderPlacementResult Failed(string reason) => new(false, null, reason);
    }

    public sealed record CheckoutLine(int ProductId, int Quantity, decimal UnitPrice);

    public sealed record CheckoutCustomer(
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
