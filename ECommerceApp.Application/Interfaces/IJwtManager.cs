using System.Collections.Generic;

namespace ECommerceApp.Application.Interfaces
{
    public interface IJwtManager
    {
        string IssueToken(string userId, string email, IEnumerable<string> roles);
    }
}
