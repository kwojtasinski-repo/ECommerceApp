using System;

namespace ECommerceApp.Domain.AccountProfile
{
    public sealed class DomainException : Exception
    {
        public DomainException(string message) : base(message) { }
    }
}
