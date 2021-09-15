using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels;
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
        private readonly ICustomerRepository _custRepo;
        private readonly IMapper _mapper;

        public CustomerService(ICustomerRepository custRepo, IMapper mapper) : base(custRepo, mapper)
        {
            _custRepo = custRepo;
            _mapper = mapper;
        }

        public int AddCustomer(NewCustomerVm newCustomer)
        {
            if (newCustomer.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customer = _mapper.Map<Domain.Model.Customer>(newCustomer);
            var id = _custRepo.Add(customer);
            return id;
        }

        public void DeleteCustomer(int id)
        {
            Delete(id);
        }
        
        public ListForCustomerVm GetAllCustomersForList(int pageSize, int pageNo, string searchString)
        {
            var customers = _custRepo.GetAllCustomers()
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
                Count = customersToShow.Count
            };

            return customersList;
        }

        public List<NewCustomerVm> GetAllCustomersForList()
        {
            var customers = _custRepo.GetAllCustomers()
                .ProjectTo<NewCustomerVm>(_mapper.ConfigurationProvider)
                .ToList();

            return customers;
        }

        public CustomerDetailsVm GetCustomerDetails(int customerId)
        {
            var customer = _custRepo.GetCustomerById(customerId);
            var customerVm = _mapper.Map<CustomerDetailsVm>(customer); 

            return customerVm;
        }

        public CustomerDetailsVm GetCustomerDetails(int id, string userId)
        {
            var customer = _custRepo.GetCustomerById(id, userId);
            var customerVm = _mapper.Map<CustomerDetailsVm>(customer);

            return customerVm;
        }
        public NewCustomerVm GetCustomerForEdit(int id)
        {
            var customer = _custRepo.GetCustomerById(id);
            var customerVm = _mapper.Map<NewCustomerVm>(customer);

            return customerVm;
        }

        public void UpdateCustomer(NewCustomerVm model)
        {
            var customer = _mapper.Map<Customer>(model);
            _custRepo.UpdateCustomer(customer);
        }

        public int CreateNewDetailContact(NewContactDetailVm newContact)
        {
            if (newContact.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var contactDetail = _mapper.Map<ContactDetail>(newContact);
            var id = _custRepo.AddNewContact(contactDetail);
            return id;
        }

        public int CreateNewAddress(NewAddressVm newAddress)
        {
            if (newAddress.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var address = _mapper.Map<Address>(newAddress);
            var id = _custRepo.AddNewAddress(address);
            return id;
        }

        public NewAddressVm GetAddressForEdit(int id)
        {
            var address = _custRepo.GetAddressById(id);
            var addresVm = _mapper.Map<NewAddressVm>(address);
            return addresVm;
        }

        public NewContactDetailVm GetContactDetail(int id)
        {
            var contactDetail = _custRepo.GetContactDetailById(id);
            var contactDetailVm = _mapper.Map<NewContactDetailVm>(contactDetail);
            return contactDetailVm;
        }

        public NewContactDetailVm GetContactDetail(int id, string userId)
        {
            var contactDetail = _custRepo.GetContactDetailById(id, userId);
            var contactDetailVm = _mapper.Map<NewContactDetailVm>(contactDetail);
            return contactDetailVm;
        }

        public void UpdateAddress(NewAddressVm model)
        {
            var address = _mapper.Map<Address>(model);
            _custRepo.UpdateAddress(address);
        }

        public void UpdateContactDetail(NewContactDetailVm model)
        {
            var contactDetail = _mapper.Map<ContactDetail>(model);
            _custRepo.UpdateContactDetail(contactDetail);
        }

        public void UpdateContactDetailType(NewContactDetailTypeVm model)
        {
            var contactDetailType = _mapper.Map<ContactDetailType>(model);
            _custRepo.UpdateContactDetailType(contactDetailType);
        }

        public void DeleteAddress(int id)
        {
            _custRepo.DeleteAddress(id);
        }

        public void DeleteContactDetail(int id)
        {
            _custRepo.DeleteContactDetail(id);
        }

        public AddressDetailVm GetAddressDetail(int id)
        {
            var adress = _custRepo.GetAddressById(id);
            var adressVm = _mapper.Map<AddressDetailVm>(adress);
            return adressVm;
        }

        public AddressDetailVm GetAddressDetail(int id, string userId)
        {
            var adress = _custRepo.GetAddressById(id, userId);
            var adressVm = _mapper.Map<AddressDetailVm>(adress);
            return adressVm;
        }

        public bool CheckIfAddressExists(int id, string userId)
        {
            var address = _custRepo.GetAddressById(id, userId);
            
            if(address == null)
            {
                return false;
            }

            return true;
        }

        public IQueryable<ContactDetailTypeVm> GetConactDetailTypes()
        {
            var contactDetailTypes = _custRepo.GetAllDetailTypes();
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
            var customer = _custRepo.GetCustomerById(id, userId);
            
            if (customer == null)
            {
                return false;
            }

            return true;
        }

        public bool CheckIfContactDetailExists(int id, string userId)
        {
            var contactDetail = _custRepo.GetContactDetailById(id, userId);

            if (contactDetail == null)
            {
                return false;
            }

            return true;
        }

        public bool CheckIfContactDetailType(int id)
        {
            var contactDetailType = _custRepo.GetContactDetailTypeById(id);

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
            var id = _custRepo.AddNewAddress(address);
            return id;
        }

        public int AddAddress(NewAddressVm model, string userId)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customers = _custRepo.GetAllCustomers().Where(c => c.UserId == userId).ToList();
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
                var id = _custRepo.AddNewAddress(address);
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
            var id = _custRepo.AddNewContact(contactDetail);
            return id;
        }

        public int AddContactDetail(NewContactDetailVm model, string userId)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customersId = _custRepo.GetAllCustomers().Where(c => c.UserId == userId).Select(c => c.Id).ToList();
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
                var id = _custRepo.AddNewContact(contactDetail);
                return id;
            }
            else
            {
                throw new BusinessException("Customer not exists check your id");
            }
        }

        public int AddContactDetailType(NewContactDetailTypeVm newContactDetailType)
        {
            if (newContactDetailType.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var contactDetailType = _mapper.Map<ContactDetailType>(newContactDetailType);
            var id = _custRepo.AddNewContactDetailType(contactDetailType);
            return id;
        }

        public NewContactDetailTypeVm GetContactDetailType(int id)
        {
            var contactDetailType = _custRepo.GetContactDetailTypeById(id);
            var contactDetailTypeVm = _mapper.Map<NewContactDetailTypeVm>(contactDetailType);
            return contactDetailTypeVm;
        }

        public void UpdateContactDetail(ContactDetailVm model)
        {
            var contactDetail = _mapper.Map<ContactDetail>(model);
            _custRepo.UpdateContactDetail(contactDetail);
        }

        public int AddContactDetail(ContactDetailVm model, string userId)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customersId = _custRepo.GetAllCustomers().Where(c => c.UserId == userId).Select(c => c.Id).ToList();
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
                var id = _custRepo.AddNewContact(contactDetail);
                return id;
            }
            else
            {
                throw new BusinessException("Customer not exists check your id");
            }
        }
    }
}
