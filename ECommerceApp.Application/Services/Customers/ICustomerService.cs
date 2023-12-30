using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Customers
{
    public interface ICustomerService
    {
        ListForCustomerVm GetAllCustomersForList(int pageSize, int pageNo, string searchString);
        ListForCustomerVm GetAllCustomersForList(string userId, int pageSize, int pageNo, string searchString);
        int AddCustomer(CustomerDto newCustomer);
        int AddCustomerDetails(CustomerDetailsDto newCustomer);
        CustomerDetailsVm GetCustomerDetails(int customerId);
        CustomerInformationForOrdersVm GetCustomerInformationById(int customerId);
        CustomerDetailsDto GetCustomerForEdit(int id);
        void UpdateCustomer(CustomerDto model);
        bool DeleteCustomer(int id);
        IEnumerable<CustomerDto> GetAllCustomers(Expression<Func<Customer, bool>> expression);
        bool CustomerExists(int id, string userId);
        CustomerDetailsVm GetCustomerDetails(int id, string userId);
        IQueryable<CustomerInformationForOrdersVm> GetCustomersInformationByUserId(string userId);
    }
}
