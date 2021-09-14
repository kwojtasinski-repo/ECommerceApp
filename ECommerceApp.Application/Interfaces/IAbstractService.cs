using ECommerceApp.Application.ViewModels;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IAbstractService<T, U, E>
    {
        T Get(int id);
        void Update(T vm);
        void Delete(T vm);
        void Delete(int id);
        int Add(T vm);
    }
}
