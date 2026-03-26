using System.Collections.Generic;
using System.Security.Claims;

namespace ECommerceApp.Application.Interfaces
{
    public interface IJwtManager
    {
        IssuedJwt IssueToken(string userId, string email, IEnumerable<string> roles, IEnumerable<Claim>? extraClaims = null);
    }
}
