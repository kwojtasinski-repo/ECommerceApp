using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Application.ViewModels.ContactDetailType;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class CustomerService : AbstractService<CustomerVm, ICustomerRepository, Customer>, ICustomerService
    {

        public CustomerService(ICustomerRepository custRepo, IMapper mapper) : base(custRepo, mapper)
        {
        }

        public int AddCustomer(NewCustomerVm newCustomer)
        {
            if (newCustomer.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customer = _mapper.Map<Domain.Model.Customer>(newCustomer);
            var id = _repo.Add(customer);
            return id;
        }

        public void DeleteCustomer(int id)
        {
            Delete(id);
        }

        public ListForCustomerVm GetAllCustomersForList(int pageSize, int pageNo, string searchString)
        {
            var customers = _repo.GetAllCustomers()
                .Where(p => p.FirstName.StartsWith(searchString) || p.LastName.StartsWith(searchString)
                || p.CompanyName.StartsWith(searchString) || p.NIP.StartsWith(searchString));

            var customersToShow = customers.Skip(pageSize * (pageNo - 1)).Take(pageSize)
                .ProjectTo<CustomerForListVm>(_mapper.ConfigurationProvider)
                .ToList();

            var customersList = new ListForCustomerVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Customers = customersToShow,
                Count = customers.Count()
            };

            return customersList;
        }

        public ListForCustomerVm GetAllCustomersForList(string userId, int pageSize, int pageNo, string searchString)
        {
            var customers = _repo.GetAllCustomers().Where(c => c.UserId == userId)
                    .Where(p => p.FirstName.StartsWith(searchString) || p.LastName.StartsWith(searchString)
                    || p.CompanyName.StartsWith(searchString) || p.NIP.StartsWith(searchString))
                    .ProjectTo<CustomerForListVm>(_mapper.ConfigurationProvider);

            var customersToShow = customers.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var customersList = new ListForCustomerVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Customers = customersToShow,
                Count = customers.Count()
            };

            return customersList;
        }

        public List<NewCustomerVm> GetAllCustomersForList()
        {
            var customers = _repo.GetAllCustomers()
                .ProjectTo<NewCustomerVm>(_mapper.ConfigurationProvider)
                .ToList();

            return customers;
        }

        public CustomerDetailsVm GetCustomerDetails(int customerId)
        {
            var customer = _repo.GetCustomerById(customerId);
            var customerVm = _mapper.Map<CustomerDetailsVm>(customer); 

            return customerVm;
        }

        public CustomerDetailsVm GetCustomerDetails(int id, string userId)
        {
            var customer = _repo.GetCustomerById(id, userId);
            var customerVm = _mapper.Map<CustomerDetailsVm>(customer);

            return customerVm;
        }
        public NewCustomerVm GetCustomerForEdit(int id)
        {
            var customer = _repo.GetCustomerById(id);
            var customerVm = _mapper.Map<NewCustomerVm>(customer);

            return customerVm;
        }

        public void UpdateCustomer(NewCustomerVm model)
        {
            var customer = _mapper.Map<Customer>(model);
            _repo.UpdateCustomer(customer);
        }

        public int CreateNewDetailContact(NewContactDetailVm newContact)
        {
            if (newContact.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var contactDetail = _mapper.Map<ContactDetail>(newContact);
            var id = _repo.AddNewContact(contactDetail);
            return id;
        }

        public int CreateNewAddress(NewAddressVm newAddress)
        {
            if (newAddress.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var address = _mapper.Map<Address>(newAddress);
            var id = _repo.AddNewAddress(address);
            return id;
        }

        public NewAddressVm GetAddressForEdit(int id)
        {
            var address = _repo.GetAddressById(id);
            var addresVm = _mapper.Map<NewAddressVm>(address);
            return addresVm;
        }

        public NewContactDetailVm GetContactDetail(int id)
        {
            var contactDetail = _repo.GetContactDetailById(id);
            var contactDetailVm = _mapper.Map<NewContactDetailVm>(contactDetail);
            return contactDetailVm;
        }

        public NewContactDetailVm GetContactDetail(int id, string userId)
        {
            var contactDetail = _repo.GetContactDetailById(id, userId);
            var contactDetailVm = _mapper.Map<NewContactDetailVm>(contactDetail);
            return contactDetailVm;
        }

        public void UpdateAddress(NewAddressVm model)
        {
            var address = _mapper.Map<Address>(model);
            _repo.UpdateAddress(address);
        }

        public void UpdateContactDetail(NewContactDetailVm model)
        {
            var contactDetail = _mapper.Map<ContactDetail>(model);
            _repo.UpdateContactDetail(contactDetail);
        }

        public void UpdateContactDetailType(ContactDetailTypeVm model)
        {
            var contactDetailType = _mapper.Map<ContactDetailType>(model);
            _repo.UpdateContactDetailType(contactDetailType);
        }

        public void DeleteAddress(int id)
        {
            _repo.DeleteAddress(id);
        }

        public void DeleteContactDetail(int id)
        {
            _repo.DeleteContactDetail(id);
        }

        public AddressDetailVm GetAddressDetail(int id)
        {
            var adress = _repo.GetAddressById(id);
            var adressVm = _mapper.Map<AddressDetailVm>(adress);
            return adressVm;
        }

        public AddressDetailVm GetAddressDetail(int id, string userId)
        {
            var adress = _repo.GetAddressById(id, userId);
            var adressVm = _mapper.Map<AddressDetailVm>(adress);
            return adressVm;
        }

        public bool CheckIfAddressExists(int id, string userId)
        {
            var address = _repo.GetAddressById(id, userId);
            
            if(address == null)
            {
                return false;
            }

            return true;
        }

        public IQueryable<ContactDetailTypeVm> GetConactDetailTypes()
        {
            var contactDetailTypes = _repo.GetAllDetailTypes();
            var contactDetailTypesVm = contactDetailTypes.ProjectTo<ContactDetailTypeVm>(_mapper.ConfigurationProvider);
            return contactDetailTypesVm;
        }

        private static string CreateRandomUser(int length = 10)
        {
            // Create a string of characters, numbers, special characters that allowed in the password  
            string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Random random = new Random();

            // Select one random character at a time from the string  
            // and create an array of chars  
            char[] chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = validChars[random.Next(0, validChars.Length)];
            }
            return new string(chars);
        }

        private static string CreateRandomPassword(int length = 15)
        {
            // Create a string of characters, numbers, special characters that allowed in the password  
            string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*?_-";
            Random random = new Random();

            // Select one random character at a time from the string  
            // and create an array of chars  
            char[] chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = validChars[random.Next(0, validChars.Length)];
            }
            return new string(chars);
        }

        public bool CheckIfCustomerExists(int id, string userId)
        {
            var customer = _repo.GetCustomerById(id, userId);
            
            if (customer == null)
            {
                return false;
            }

            return true;
        }

        public bool CheckIfContactDetailExists(int id, string userId)
        {
            var contactDetail = _repo.GetContactDetailById(id, userId);

            if (contactDetail == null)
            {
                return false;
            }

            return true;
        }

        public bool CheckIfContactDetailType(int id)
        {
            var contactDetailType = _repo.GetContactDetailTypeById(id);

            if (contactDetailType == null)
            {
                return false;
            }

            return true;
        }

        public int AddAddress(NewAddressVm newAddress)
        {
            if (newAddress.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var address = _mapper.Map<Address>(newAddress);
            var id = _repo.AddNewAddress(address);
            return id;
        }

        public int AddAddress(NewAddressVm model, string userId)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customers = _repo.GetAllCustomers().Where(c => c.UserId == userId).ToList();
            bool customerIdExists = false;
            foreach (var cust in customers)
            {
                if (cust.Id == model.CustomerId)
                {
                    customerIdExists = true;
                    break;
                }
            }

            if (customerIdExists)
            {
                var address = _mapper.Map<Address>(model);
                var id = _repo.AddNewAddress(address);
                return id;
            }
            else
            {
                return 0;
            }
        }

        public int AddContactDetail(NewContactDetailVm newContactDetail)
        {
            if (newContactDetail.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var contactDetail = _mapper.Map<ContactDetail>(newContactDetail);
            var id = _repo.AddNewContact(contactDetail);
            return id;
        }

        public int AddContactDetail(NewContactDetailVm model, string userId)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customersId = _repo.GetAllCustomers().Where(c => c.UserId == userId).Select(c => c.Id).ToList();
            bool customerIdExists = false;
            foreach (var custId in customersId)
            {
                if (custId == model.CustomerId)
                {
                    customerIdExists = true;
                    break;
                }
            }

            if (customerIdExists)
            {
                var contactDetail = _mapper.Map<ContactDetail>(model);
                var id = _repo.AddNewContact(contactDetail);
                return id;
            }
            else
            {
                throw new BusinessException("Customer not exists check your id");
            }
        }

        public int AddContactDetailType(ContactDetailTypeVm newContactDetailType)
        {
            if (newContactDetailType.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var contactDetailType = _mapper.Map<ContactDetailType>(newContactDetailType);
            var id = _repo.AddNewContactDetailType(contactDetailType);
            return id;
        }

        public ContactDetailTypeVm GetContactDetailType(int id)
        {
            var contactDetailType = _repo.GetContactDetailTypeById(id);
            var contactDetailTypeVm = _mapper.Map<ContactDetailTypeVm>(contactDetailType);
            return contactDetailTypeVm;
        }

        public void UpdateContactDetail(ContactDetailVm model)
        {
            var contactDetail = _mapper.Map<ContactDetail>(model);
            _repo.UpdateContactDetail(contactDetail);
        }

        public int AddContactDetail(ContactDetailVm model, string userId)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customersId = _repo.GetAllCustomers().Where(c => c.UserId == userId).Select(c => c.Id).ToList();
            bool customerIdExists = false;
            foreach (var custId in customersId)
            {
                if (custId == model.CustomerId)
                {
                    customerIdExists = true;
                    break;
                }
            }

            if (customerIdExists)
            {
                var contactDetail = _mapper.Map<ContactDetail>(model);
                var id = _repo.AddNewContact(contactDetail);
                return id;
            }
            else
            {
                throw new BusinessException("Customer not exists check your id");
            }
        }
    }
}
