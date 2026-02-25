using ECommerceApp.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System;
using System.Text;
using System.Linq;

namespace ECommerceApp.Infrastructure.Identity.IAM.Auth
{
    internal class JwtManager : IJwtManager
    {
        private readonly IOptions<AuthOptions> _authOptions;
        private readonly SigningCredentials _signingCredentials;

        public JwtManager(IOptions<AuthOptions> authOptions)
        {
            _authOptions = authOptions;
            _signingCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authOptions.Value.Key)),
                SecurityAlgorithms.HmacSha256);
        }

        public string IssueToken(string userId, string email, IEnumerable<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, email)
            };

            claims.AddRange(roles.Select(role => new Claim(ClaimsIdentity.DefaultRoleClaimType, role)));
            var token = new JwtSecurityToken(_authOptions.Value.Issuer, _authOptions.Value.Issuer, claims, expires: DateTime.Now.AddMinutes(120), signingCredentials: _signingCredentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
