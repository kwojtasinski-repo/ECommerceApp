using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface ICustomerRepository : IGenericRepository<Customer>
    {
        void DeleteCustomer(int customerId);
        int AddCustomer(Customer customer);
        Customer GetCustomerById(int id);
        IQueryable<Customer> GetAllCustomers();
        void UpdateCustomer(Customer customer);
        Customer GetCustomerById(int id, string userId);
    }
}
