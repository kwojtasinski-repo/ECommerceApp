using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Tests.Common
{
    public class BaseServiceTest<T, R, RI, S, E> : BaseTest<E> 
        where T : BaseVm
        where R : IGenericRepository<E>
        where RI : GenericRepository<E>
        where S : IAbstractService<T, R, E>
        where E : BaseEntity
    {
        protected readonly S _service;

        public BaseServiceTest()
        {
            var serviceType = typeof(S);
            _service = (S) ImplementationUtil.GetServiceInstance<T, R, RI, E>(serviceType, _context);
        }

    }
}
