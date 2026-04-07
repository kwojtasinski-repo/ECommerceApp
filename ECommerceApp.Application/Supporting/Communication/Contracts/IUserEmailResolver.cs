using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Contracts
{
    /// <summary>
    /// Port allowing the Communication BC to resolve the email address of a user
    /// without coupling to the Identity BC's application services.
    /// Implemented by the Infrastructure Identity adapter.
    /// </summary>
    public interface IUserEmailResolver
    {
        Task<string?> GetEmailForUserAsync(string userId, CancellationToken ct = default);
    }
}
