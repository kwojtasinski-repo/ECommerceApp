using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Order;
using System.Collections.Generic;

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
        CustomerDetailsDto GetCustomer(int id);
        CustomerDetailsDto GetCustomer(int id, string userId);
        bool UpdateCustomer(CustomerDto model);
        bool DeleteCustomer(int id);
        bool CustomerExists(int id, string userId);
        CustomerDetailsVm GetCustomerDetails(int id, string userId);
        List<CustomerInformationForOrdersVm> GetCustomersInformationByUserId(string userId);
        bool ExistsById(int customerId);
    }
}
