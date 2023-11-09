using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Application.Interfaces
{
    public interface IJwtManager
    {
        string IssueToken(ApplicationUser applicationUser, IEnumerable<string> roles);
    }
}
