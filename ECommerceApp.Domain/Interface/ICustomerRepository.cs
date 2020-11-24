using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface ICustomerRepository
    {
        void DeleteCustomer(int customerId);
        int AddCustomer(Customer customer);
        Customer GetCustomerById(int id);
    }
}
