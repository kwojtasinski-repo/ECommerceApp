using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _custRepo;
        private readonly IMapper _mapper;

        public CustomerService(ICustomerRepository custRepo, IMapper mapper)
        {
            _custRepo = custRepo;
            _mapper = mapper;
        }

        public int AddCustomer(NewCustomerVm newCustomer)
        {
            CheckIfContactDetailTypeHasValues();
            var customer = _mapper.Map<Customer>(newCustomer);
            var id = _custRepo.AddCustomer(customer);
            return id;
        }

        private void CheckIfContactDetailTypeHasValues()
        {
            var detailTypes = _custRepo.GetAllDetailTypes().ToList();
            if (detailTypes.Count == 0)
            {
                ContactDetailType contactDetailTypePhone = new ContactDetailType() { Name = "PhoneNumber" };
                _custRepo.AddContactDetailType(contactDetailTypePhone);
                ContactDetailType contactDetailTypeEmail = new ContactDetailType() { Name = "Email" };
                _custRepo.AddContactDetailType(contactDetailTypeEmail);
            }
        }

        public void DeleteCustomer(int id)
        {
            _custRepo.DeleteCustomer(id);
        }
        
        public ListForCustomerVm GetAllCustomersForList(int pageSize, int pageNo, string searchString)
        {
            var customers = _custRepo.GetAllCustomers()
                .Where(p => p.FirstName.StartsWith(searchString) || p.LastName.StartsWith(searchString) 
                || p.CompanyName.StartsWith(searchString) || p.NIP.StartsWith(searchString))
                .ProjectTo<CustomerForListVm>(_mapper.ConfigurationProvider)
                .ToList(); 

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

        public CustomerDetailsVm GetCustomerDetails(int customerId)
        {
            var customer = _custRepo.GetCustomerById(customerId);
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
            var contactDetail = _mapper.Map<ContactDetail>(newContact);
            var id = _custRepo.AddNewContact(contactDetail);
            return id;
        }

        public int CreateNewAddress(NewAddressVm newAddress)
        {
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

        public IQueryable<ContactDetailTypeVm> GetConactDetailTypes()
        {
            var contactDetailTypes = _custRepo.GetAllDetailTypes();
            var contactDetailTypesVm = contactDetailTypes.ProjectTo<ContactDetailTypeVm>(_mapper.ConfigurationProvider);
            return contactDetailTypesVm;
        }
    }
}
