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
        List<CustomerForListVm> GetAllCustomersForList();
        bool CheckIfAddressExists(int id, string userId);
        bool CheckIfCustomerExists(int id, string userId);
        bool CheckIfContactDetailExists(int id, string userId);
        int AddAddress(NewAddressVm model);
        int AddContactDetail(NewContactDetailVm model);
        int AddContactDetailType(NewContactDetailTypeVm model);
        bool CheckIfContactDetailType(int id);
        void UpdateContactDetailType(NewContactDetailTypeVm model);
        NewContactDetailTypeVm GetContactDetailType(int id);
        object GetCustomerDetails(int id, string userId);
        AddressDetailVm GetAddressDetail(int id, string userId);
        NewContactDetailVm GetContactDetail(int id, string userId);
        int AddAddress(NewAddressVm model, string userId);
        int AddContactDetail(NewContactDetailVm model, string userId);
    }
}
