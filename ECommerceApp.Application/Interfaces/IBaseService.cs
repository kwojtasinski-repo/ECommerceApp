using ECommerceApp.Application.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IBaseService<T> where T : BaseVm
    {
        int Add(T objectVm); 
        void Update(T objectVm); 
        void Delete(int id); 
        T Get(int id); 
        List<T> GetAll();
        List<T> GetAll(string searchName); 
    }
}
