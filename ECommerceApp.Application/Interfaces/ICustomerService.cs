using ECommerceApp.Application.ViewModels.Customer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface ICustomerService
    {
        ListForCustomerVm GetAllCustomersForList(int pageSize, int pageNo, string searchString);
        int AddCustomer(NewCustomerVm newCustomer);
        CustomerDetailsVm GetCustomerDetails(int customerId);
        NewCustomerVm GetCustomerForEdit(int id);
        NewAddressVm GetAddressForEdit(int id);
        NewContactDetailVm GetContactDetail(int id);
        void UpdateCustomer(NewCustomerVm model);
        void DeleteCustomer(int id);
        int CreateNewDetailContact(NewContactDetailVm newContact);
        int CreateNewAddress(NewAddressVm newAddress);
        void UpdateAddress(NewAddressVm model);
        void UpdateContactDetail(NewContactDetailVm model);
        void DeleteAddress(int id);
        void DeleteContactDetail(int id);
        AddressDetailVm GetAddressDetail(int id);
        IQueryable<ContactDetailTypeVm> GetConactDetailTypes();
    }
}
