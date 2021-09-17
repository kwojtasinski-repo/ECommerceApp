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
        where T : BaseVm // ViewModel
        where R : IGenericRepository<E> // interfejs repo (ze wzgledu na serwis ktory przyjmuje interfejs)
        where RI : GenericRepository<E> // implementacja repo (potrzebna do utworzenia instancji repo)
        where S : IAbstractService<T, R, E> // konkretna implementacja serwisu
        where E : BaseEntity // encja
    {
        protected readonly S _service;

        public BaseServiceTest()
        {
            var serviceType = typeof(S);
            _service = (S) ImplementationUtil.GetServiceInstance<T, R, RI, E>(serviceType, _context);
        }

    }
}
