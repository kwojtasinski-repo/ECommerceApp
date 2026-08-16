using ECommerceApp.Domain.Supporting.Verification;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.Verification
{
    internal interface IVerificationDbContext
    {
        DbSet<VerificationCode> VerificationCodes { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}