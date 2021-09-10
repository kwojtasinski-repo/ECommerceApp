using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure;
using ECommerceApp.Infrastructure.Repositories;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Tests.Common
{
    public class BaseTest<T> : IDisposable where T : BaseEntity
    {
        private readonly Context _context;
        private readonly Mock<Context> _contextMock;
        protected readonly IAbstractRepository<T> _abstractRepository;

        public BaseTest()
        {
            _contextMock = DbContextFactory.Create();
            _context = _contextMock.Object;
            _abstractRepository = new AbstractRepository<T>(_context);
        }

        public void Dispose()
        {
            DbContextFactory.Destroy(_context);
        }
    }
}
