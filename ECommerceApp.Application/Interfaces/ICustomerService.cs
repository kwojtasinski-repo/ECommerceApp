using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Application.ViewModels.ContactDetailType;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface ICustomerService : IAbstractService<CustomerVm, ICustomerRepository, Customer>
    {
        ListForCustomerVm GetAllCustomersForList(int pageSize, int pageNo, string searchString);
        ListForCustomerVm GetAllCustomersForList(string userId, int pageSize, int pageNo, string searchString);
        int AddCustomer(NewCustomerVm newCustomer);
        CustomerDetailsVm GetCustomerDetails(int customerId);
        NewCustomerVm GetCustomerForEdit(int id);
        void UpdateCustomer(NewCustomerVm model);
        void DeleteCustomer(int id);
        IEnumerable<CustomerVm> GetAllCustomers(Expression<Func<Customer, bool>> expression);
        bool CustomerExists(int id, string userId);
        CustomerDetailsVm GetCustomerDetails(int id, string userId);
    }
}
