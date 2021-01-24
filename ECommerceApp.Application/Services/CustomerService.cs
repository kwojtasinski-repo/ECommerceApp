using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Interfaces;
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
    public class CustomerService : CustomerServiceAbstract
    {
        private readonly ICustomerRepository _custRepo;
        private readonly IMapper _mapper;

        public CustomerService(ICustomerRepository custRepo, IMapper mapper) : base(custRepo, mapper)
        {
            _custRepo = custRepo;
            _mapper = mapper;
        }

        public override int AddCustomer(NewCustomerVm newCustomer)
        {
            return Add(newCustomer);
        }

        public override void DeleteCustomer(int id)
        {
            Delete(id);
        }
        
        public override ListForCustomerVm GetAllCustomersForList(int pageSize, int pageNo, string searchString)
        {
            var customers = GetAll(searchString);

            var customersToShow = customers.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var customersList = new ListForCustomerVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Customers = customersToShow,
                Count = customers.Count
            };

            return customersList;
        }

        public override List<NewCustomerVm> GetAllCustomersForList()
        {
            return GetAll();
        }

        public override CustomerDetailsVm GetCustomerDetails(int customerId)
        {
            var customer = _custRepo.GetCustomerById(customerId);
            var customerVm = _mapper.Map<CustomerDetailsVm>(customer); 

            return customerVm;
        }

        public override CustomerDetailsVm GetCustomerDetails(int id, string userId)
        {
            var customer = _custRepo.GetCustomerById(id, userId);
            var customerVm = _mapper.Map<CustomerDetailsVm>(customer);

            return customerVm;
        }
        public override NewCustomerVm GetCustomerForEdit(int id)
        {
            return Get(id);
        }

        public override void UpdateCustomer(NewCustomerVm model)
        {
            Update(model);
        }

        public override int CreateNewDetailContact(NewContactDetailVm newContact)
        {
            var contactDetail = _mapper.Map<ContactDetail>(newContact);
            var id = _custRepo.AddNewContact(contactDetail);
            return id;
        }

        public override int CreateNewAddress(NewAddressVm newAddress)
        {
            var address = _mapper.Map<Address>(newAddress);
            var id = _custRepo.AddNewAddress(address);
            return id;
        }

        public override NewAddressVm GetAddressForEdit(int id)
        {
            var address = _custRepo.GetAddressById(id);
            var addresVm = _mapper.Map<NewAddressVm>(address);
            return addresVm;
        }

        public override NewContactDetailVm GetContactDetail(int id)
        {
            var contactDetail = _custRepo.GetContactDetailById(id);
            var contactDetailVm = _mapper.Map<NewContactDetailVm>(contactDetail);
            return contactDetailVm;
        }

        public override NewContactDetailVm GetContactDetail(int id, string userId)
        {
            var contactDetail = _custRepo.GetContactDetailById(id, userId);
            var contactDetailVm = _mapper.Map<NewContactDetailVm>(contactDetail);
            return contactDetailVm;
        }

        public override void UpdateAddress(NewAddressVm model)
        {
            var address = _mapper.Map<Address>(model);
            _custRepo.UpdateAddress(address);
        }

        public override void UpdateContactDetail(NewContactDetailVm model)
        {
            var contactDetail = _mapper.Map<ContactDetail>(model);
            _custRepo.UpdateContactDetail(contactDetail);
        }

        public override void UpdateContactDetailType(NewContactDetailTypeVm model)
        {
            var contactDetailType = _mapper.Map<ContactDetailType>(model);
            _custRepo.UpdateContactDetailType(contactDetailType);
        }

        public override void DeleteAddress(int id)
        {
            _custRepo.DeleteAddress(id);
        }

        public override void DeleteContactDetail(int id)
        {
            _custRepo.DeleteContactDetail(id);
        }

        public override AddressDetailVm GetAddressDetail(int id)
        {
            var adress = _custRepo.GetAddressById(id);
            var adressVm = _mapper.Map<AddressDetailVm>(adress);
            return adressVm;
        }

        public override AddressDetailVm GetAddressDetail(int id, string userId)
        {
            var adress = _custRepo.GetAddressById(id, userId);
            var adressVm = _mapper.Map<AddressDetailVm>(adress);
            return adressVm;
        }

        public override bool CheckIfAddressExists(int id, string userId)
        {
            var address = _custRepo.GetAddressById(id, userId);
            
            if(address == null)
            {
                return false;
            }

            return true;
        }

        public override IQueryable<ContactDetailTypeVm> GetConactDetailTypes()
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

        public override bool CheckIfCustomerExists(int id, string userId)
        {
            var customer = _custRepo.GetCustomerById(id, userId);
            
            if (customer == null)
            {
                return false;
            }

            return true;
        }

        public override bool CheckIfContactDetailExists(int id, string userId)
        {
            var contactDetail = _custRepo.GetContactDetailById(id, userId);

            if (contactDetail == null)
            {
                return false;
            }

            return true;
        }

        public override bool CheckIfContactDetailType(int id)
        {
            var contactDetailType = _custRepo.GetContactDetailTypeById(id);

            if (contactDetailType == null)
            {
                return false;
            }

            return true;
        }

        public override int AddAddress(NewAddressVm newAddress)
        {
            var address = _mapper.Map<Address>(newAddress);
            var id = _custRepo.AddNewAddress(address);
            return id;
        }

        public override int AddAddress(NewAddressVm model, string userId)
        {
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

        public override int AddContactDetail(NewContactDetailVm newContactDetail)
        {
            var contactDetail = _mapper.Map<ContactDetail>(newContactDetail);
            var id = _custRepo.AddNewContact(contactDetail);
            return id;
        }

        public override int AddContactDetail(NewContactDetailVm model, string userId)
        {
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
                var contactDetail = _mapper.Map<ContactDetail>(model);
                var id = _custRepo.AddNewContact(contactDetail);
                return id;
            }
            else
            {
                return 0;
            }
        }

        public override int AddContactDetailType(NewContactDetailTypeVm newContactDetailType)
        {
            var contactDetailType = _mapper.Map<ContactDetailType>(newContactDetailType);
            var id = _custRepo.AddNewContactDetailType(contactDetailType);
            return id;
        }

        public override NewContactDetailTypeVm GetContactDetailType(int id)
        {
            var contactDetailType = _custRepo.GetContactDetailTypeById(id);
            var contactDetailTypeVm = _mapper.Map<NewContactDetailTypeVm>(contactDetailType);
            return contactDetailTypeVm;
        }
    }
}
