using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Contracts
{
    public interface IAccountProfileClient
    {
        Task<CheckoutProfileVm?> GetProfileAsync(string userId, CancellationToken ct = default);
    }

    public sealed record CheckoutProfileVm(
        int CustomerId,
        string FirstName,
        string LastName,
        string Email,
        string PhoneNumber,
        bool IsCompany,
        string? CompanyName,
        string? Nip,
        string? Street,
        string? BuildingNumber,
        string? FlatNumber,
        string? ZipCode,
        string? City,
        string? Country);
}
