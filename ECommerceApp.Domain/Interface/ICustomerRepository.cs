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
        IQueryable<ContactDetailType> GetAllDetailTypes();
        int AddContactDetailType(ContactDetailType contactDetailType);
        int AddNewContact(ContactDetail contactDetail);
        int AddNewAddress(Address address);
        Address GetAddressById(int id);
        ContactDetail GetContactDetailById(int id);
        void UpdateAddress(Address address);
        void UpdateContactDetail(ContactDetail contactDetail);
        void DeleteAddress(int id);
        void DeleteContactDetail(int id);
        int AddNewContactDetailType(ContactDetailType contactDetailType);
        ContactDetailType GetContactDetailTypeById(int id);
        void UpdateContactDetailType(ContactDetailType contactDetailType);
        Customer GetCustomerById(int id, string userId);
        Address GetAddressById(int id, string userId);
        ContactDetail GetContactDetailById(int id, string userId);
    }
}
