using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface ICustomerRepository
    {
        bool DeleteCustomer(int customerId);
        int AddCustomer(Customer customer);
        Customer GetCustomerDetailsById(int id);
        Customer GetCustomerById(int id);
        void UpdateCustomer(Customer customer);
        Customer GetCustomerDetailsById(int id, string userId);
        Customer GetCustomerById(int id, string userId);
        bool CustomerExists(int id, string userId);
        List<Customer> GetCustomersByUserId(string userId);
        Customer GetById(int customerId);
        List<Customer> GetAllUserCustomers(string userId, int pageSize, int pageNo, string searchString);
        int GetCountBySearchStringAndUserId(string searchString, string userId);
        List<Customer> GetAllCustomers(int pageSize, int pageNo, string searchString);
        int GetCountBySearchString(string searchString);
        List<Customer> GetAllUserCustomers(string userId);
        bool ExistsById(int customerId);
    }
}
