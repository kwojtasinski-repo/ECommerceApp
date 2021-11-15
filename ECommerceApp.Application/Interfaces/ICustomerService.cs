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
        AddressVm GetAddressForEdit(int id);
        NewContactDetailVm GetContactDetail(int id);
        void UpdateCustomer(NewCustomerVm model);
        void DeleteCustomer(int id);
        int CreateNewDetailContact(NewContactDetailVm newContact);
        int CreateNewAddress(AddressVm newAddress);
        void UpdateAddress(AddressVm model);
        void UpdateContactDetail(NewContactDetailVm model);
        void DeleteAddress(int id);
        void DeleteContactDetail(int id);
        AddressVm GetAddressDetail(int id);
        IEnumerable<CustomerVm> GetAllCustomers(Expression<Func<Customer, bool>> expression);
        IQueryable<ContactDetailTypeVm> GetConactDetailTypes();
        bool CheckIfAddressExists(int id, string userId);
        bool CustomerExists(int id, string userId);
        bool CheckIfContactDetailExists(int id, string userId);
        int AddAddress(AddressVm model);
        int AddContactDetail(NewContactDetailVm model);
        int AddContactDetailType(ContactDetailTypeVm model);
        bool CheckIfContactDetailType(int id);
        void UpdateContactDetailType(ContactDetailTypeVm model);
        CustomerDetailsVm GetCustomerDetails(int id, string userId);
        ContactDetailTypeVm GetContactDetailType(int id);
        AddressVm GetAddressDetail(int id, string userId);
        NewContactDetailVm GetContactDetail(int id, string userId);
        int AddAddress(AddressVm model, string userId);
        int AddContactDetail(NewContactDetailVm model, string userId);
        void UpdateContactDetail(ContactDetailVm model);
        int AddContactDetail(ContactDetailVm model, string userId);
    }
}
