using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface ICustomerRepository : IGenericRepository<Customer>
    {
        bool DeleteCustomer(int customerId);
        int AddCustomer(Customer customer);
        Customer GetCustomerById(int id);
        IQueryable<Customer> GetAllCustomers();
        void UpdateCustomer(Customer customer);
        Customer GetCustomerById(int id, string userId);
        bool CustomerExists(int id, string userId);
        List<Customer> GetCustomersByUserId(string userId);
    }
}
