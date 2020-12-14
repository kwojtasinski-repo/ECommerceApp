using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly Context _context;
        public CustomerRepository(Context context)
        {
            _context = context;
        }

        public void DeleteCustomer(int customerId)
        {
            var customer = _context.Customers.Find(customerId);


            if (customer != null)
            {
                var customerWithOrders = _context.Customers.Include(p => p.Orders)
                                         .SingleOrDefault(p => p.Id == customerId);

                foreach (var order in customerWithOrders.Orders.ToList())
                {
                    _context.Orders.Remove(order);
                }

                var customerWithAddresses = _context.Customers.Include(p => p.Addresses)
                                            .SingleOrDefault(p => p.Id == customerId);

                foreach (var address in customerWithAddresses.Addresses.ToList())
                {
                    _context.Addresses.Remove(address);
                }

                var customerWithContactDetails = _context.Customers.Include(p => p.ContactDetails)
                                            .SingleOrDefault(p => p.Id == customerId);

                foreach (var contactDetail in customerWithContactDetails.ContactDetails.ToList())
                {
                    _context.ContactDetails.Remove(contactDetail);
                }

                var customerWithPayments = _context.Customers.Include(p => p.Payments)
                                            .SingleOrDefault(p => p.Id == customerId);

                foreach (var payment in customerWithPayments.Payments.ToList())
                {
                    _context.Payments.Remove(payment);
                }

                var customerWithRefunds = _context.Customers.Include(p => p.Refunds)
                                            .SingleOrDefault(p => p.Id == customerId);

                foreach (var refund in customerWithRefunds.Refunds.ToList())
                {
                    _context.Refunds.Remove(refund);
                }

                _context.Customers.Remove(customer);
                _context.SaveChanges();
            }
        }

        public int AddCustomer(Customer customer)
        {
            _context.Customers.Add(customer);
            _context.SaveChanges();
            return customer.Id;
        }

        public Customer GetCustomerById(int id)
        {
            var customer = _context.Customers
                .Include(inc => inc.ContactDetails)
                .Include(inc => inc.ContactDetails).ThenInclude(inc => inc.ContactDetailType)
                .Include(inc => inc.Addresses)
                .FirstOrDefault(c => c.Id == id);
            return customer;
        }

        public IQueryable<Customer> GetAllCustomers()
        {
            return _context.Customers;
        }

        public void UpdateCustomer(Customer customer)
        {
            _context.Attach(customer);
            _context.Entry(customer).Property("FirstName").IsModified = true;
            _context.Entry(customer).Property("LastName").IsModified = true;
            _context.Entry(customer).Property("IsCompany").IsModified = true;
            _context.Entry(customer).Property("NIP").IsModified = true;
            _context.Entry(customer).Property("CompanyName").IsModified = true;
            _context.Entry(customer).Collection("ContactDetails").IsModified = true;
            _context.Entry(customer).Collection("Addresses").IsModified = true;
            _context.SaveChanges();
        }

        public IQueryable<ContactDetailType> GetAllDetailTypes()
        {
            return _context.ContactDetailTypes;
        }

        public int AddContactDetailType(ContactDetailType contactDetailType)
        {
            _context.ContactDetailTypes.Add(contactDetailType);
            _context.SaveChanges();
            return contactDetailType.Id;
        }

        public int AddNewContact(ContactDetail contactDetail)
        {
            _context.ContactDetails.Add(contactDetail);
            _context.SaveChanges();
            return contactDetail.Id;
        }

        public int AddNewAddress(Address address)
        {
            _context.Addresses.Add(address);
            _context.SaveChanges();
            return address.Id;
        }

        public Address GetAddressById(int id)
        {
            var address = _context.Addresses.FirstOrDefault(c => c.Id == id);
            return address;
        }

        public ContactDetail GetContactDetailById(int id)
        {
            var contactDetail = _context.ContactDetails
                .Include(inc => inc.ContactDetailType)
                .FirstOrDefault(c => c.Id == id);
            return contactDetail;
        }

        public void UpdateAddress(Address address)
        {
            _context.Attach(address);
            _context.Entry(address).Property("Street").IsModified = true;
            _context.Entry(address).Property("BuildingNumber").IsModified = true;
            _context.Entry(address).Property("FlatNumber").IsModified = true;
            _context.Entry(address).Property("ZipCode").IsModified = true;
            _context.Entry(address).Property("City").IsModified = true;
            _context.Entry(address).Property("Country").IsModified = true;
            _context.SaveChanges();
    }

        public void UpdateContactDetail(ContactDetail contactDetail)
        {
            _context.Attach(contactDetail);
            _context.Entry(contactDetail).Property("ContactDetailInformation").IsModified = true;
            _context.Entry(contactDetail).Property("ContactDetailTypeId").IsModified = true;
            _context.SaveChanges();
    }

        public void DeleteAddress(int id)
        {
            var address = _context.Addresses.Find(id);

            if (address != null)
            {
                _context.Addresses.Remove(address);
                _context.SaveChanges();
            }
        }

        public void DeleteContactDetail(int id)
        {
            var contactDetail = _context.ContactDetails.Find(id);

            if (contactDetail != null)
            {
                _context.ContactDetails.Remove(contactDetail);
                _context.SaveChanges();
            }
        }
    }
}
