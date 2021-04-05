using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests.Repositories.CustomerRepository
{
    public class CustomerRepositoryTests
    {
        [Fact]
        public void CanReturnCustomerByIdFromDb()
        {
            var customersInMemoryDatabase = new List<Customer>
            {
                new Customer() { Id = 1, FirstName = "John", LastName = "Travolta" },
                new Customer() { Id = 2, FirstName = "Alicia", LastName = "Vikander" },
                new Customer() { Id = 3, FirstName = "Olga", LastName = "Kurylenko" },
                new Customer() { Id = 4, FirstName = "Natalie", LastName = "Portman" },
                new Customer() { Id = 5, FirstName = "Jenifer", LastName = "Aniston" },
                new Customer() { Id = 6, FirstName = "Jason", LastName = "Statham" },
                new Customer() { Id = 7, FirstName = "Scarlett", LastName = "Johanson" },
                new Customer() { Id = 8, FirstName = "Keanu", LastName = "Reeves" }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetCustomerById(It.IsAny<int>())).Returns((int id) => customersInMemoryDatabase.SingleOrDefault(i => i.Id == id));
            var repository = mock.Object;

            var customerThatExists = repository.GetCustomerById(4);
            customerThatExists.Should().NotBeNull();
            customerThatExists.Should().BeSameAs(customersInMemoryDatabase[3]);
            customerThatExists.Should().Be(customersInMemoryDatabase[3]);
            customerThatExists.Should().BeOfType(typeof(Customer));
        }

        [Fact]
        public void CantReturnCustomerByIdFromDb()
        {
            var customersInMemoryDatabase = new List<Customer>
            {
                new Customer() { Id = 1, FirstName = "John", LastName = "Travolta" },
                new Customer() { Id = 2, FirstName = "Alicia", LastName = "Vikander" },
                new Customer() { Id = 3, FirstName = "Olga", LastName = "Kurylenko" },
                new Customer() { Id = 4, FirstName = "Natalie", LastName = "Portman" },
                new Customer() { Id = 5, FirstName = "Jenifer", LastName = "Aniston" },
                new Customer() { Id = 6, FirstName = "Jason", LastName = "Statham" },
                new Customer() { Id = 7, FirstName = "Scarlett", LastName = "Johanson" },
                new Customer() { Id = 8, FirstName = "Keanu", LastName = "Reeves" }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetCustomerById(It.IsAny<int>())).Returns((int id) => customersInMemoryDatabase.SingleOrDefault(i => i.Id == id));
            var repository = mock.Object;

            var customerThatExists = repository.GetCustomerById(10);
            customerThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnCustomerByIdUserIdFromDb()
        {
            var customersInMemoryDatabase = new List<Customer>
            {
                new Customer() { Id = 1, FirstName = "John", LastName = "Travolta", UserId = "JohnTravolta" },
                new Customer() { Id = 2, FirstName = "Alicia", LastName = "Vikander", UserId = "AliciaVikander" },
                new Customer() { Id = 3, FirstName = "Olga", LastName = "Kurylenko", UserId = "OlgaKurylenko" },
                new Customer() { Id = 4, FirstName = "Natalie", LastName = "Portman", UserId = "NataliePortman" },
                new Customer() { Id = 5, FirstName = "Jenifer", LastName = "Aniston", UserId = "JeniferAniston" },
                new Customer() { Id = 6, FirstName = "Jason", LastName = "Statham", UserId = "JasonStatham" },
                new Customer() { Id = 7, FirstName = "Scarlett", LastName = "Johanson", UserId = "ScarlettJohanson" },
                new Customer() { Id = 8, FirstName = "Keanu", LastName = "Reeves", UserId = "KeanuReeves" }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetCustomerById(It.IsAny<int>(), It.IsAny<string>())).Returns((int id, string userId) => customersInMemoryDatabase.SingleOrDefault(c => c.Id == id && c.UserId == userId));
            var repository = mock.Object;

            var customerThatExists = repository.GetCustomerById(3, "OlgaKurylenko");
            customerThatExists.Should().NotBeNull();
            customerThatExists.Should().BeSameAs(customersInMemoryDatabase[2]);
            customerThatExists.Should().Be(customersInMemoryDatabase[2]);
            customerThatExists.Should().BeOfType(typeof(Customer));
        }

        [Fact]
        public void CantReturnCustomerByIdUserIdFromDb()
        {
            var customersInMemoryDatabase = new List<Customer>
            {
                new Customer() { Id = 1, FirstName = "John", LastName = "Travolta", UserId = "JohnTravolta" },
                new Customer() { Id = 2, FirstName = "Alicia", LastName = "Vikander", UserId = "AliciaVikander" },
                new Customer() { Id = 3, FirstName = "Olga", LastName = "Kurylenko", UserId = "OlgaKurylenko" },
                new Customer() { Id = 4, FirstName = "Natalie", LastName = "Portman", UserId = "NataliePortman" },
                new Customer() { Id = 5, FirstName = "Jenifer", LastName = "Aniston", UserId = "JeniferAniston" },
                new Customer() { Id = 6, FirstName = "Jason", LastName = "Statham", UserId = "JasonStatham" },
                new Customer() { Id = 7, FirstName = "Scarlett", LastName = "Johanson", UserId = "ScarlettJohanson" },
                new Customer() { Id = 8, FirstName = "Keanu", LastName = "Reeves", UserId = "KeanuReeves" }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetCustomerById(It.IsAny<int>(), It.IsAny<string>())).Returns((int id, string userId) => customersInMemoryDatabase.SingleOrDefault(c => c.Id == id && c.UserId == userId));
            var repository = mock.Object;

            var customerThatExists = repository.GetCustomerById(7, "NataliePortman");
            customerThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnCustomersFromDb()
        {
            var customersInMemoryDatabase = new List<Customer>
            {
                new Customer() { Id = 1, FirstName = "John", LastName = "Travolta" },
                new Customer() { Id = 2, FirstName = "Alicia", LastName = "Vikander" },
                new Customer() { Id = 3, FirstName = "Olga", LastName = "Kurylenko" },
                new Customer() { Id = 4, FirstName = "Natalie", LastName = "Portman" },
                new Customer() { Id = 5, FirstName = "Jenifer", LastName = "Aniston" },
                new Customer() { Id = 6, FirstName = "Jason", LastName = "Statham" },
                new Customer() { Id = 7, FirstName = "Scarlett", LastName = "Johanson" },
                new Customer() { Id = 8, FirstName = "Keanu", LastName = "Reeves" }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetAllCustomers()).Returns(customersInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var customersThatExists = repository.GetAllCustomers();
            customersThatExists.Should().NotBeNull();
            customersThatExists.Should().HaveCount(8);
        }

        [Fact]
        public void CantReturnCustomersFromDb()
        {
            var customersInMemoryDatabase = new List<Customer>();

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetAllCustomers()).Returns(customersInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var customersThatExists = repository.GetAllCustomers();
            customersThatExists.Should().NotBeNull();
            customersThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnAddressByIdFromDb()
        {
            var addressesInMemoryDatabase = new List<Address>
            {
                new Address() { Id = 1, City = "NS" },
                new Address() { Id = 2, City = "ZG" },
                new Address() { Id = 3, City = "WR" }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetAddressById(It.IsAny<int>())).Returns((int id) => addressesInMemoryDatabase.SingleOrDefault(c => c.Id == id));
            var repository = mock.Object;

            var customerThatExists = repository.GetAddressById(3);
            customerThatExists.Should().NotBeNull();
            customerThatExists.Should().BeSameAs(addressesInMemoryDatabase[2]);
            customerThatExists.Should().Be(addressesInMemoryDatabase[2]);
            customerThatExists.Should().BeOfType(typeof(Address));
        }

        [Fact]
        public void CantReturnAddressByIdFromDb()
        {
            var addressesInMemoryDatabase = new List<Address>
            {
                new Address() { Id = 1, City = "NS" },
                new Address() { Id = 2, City = "ZG" },
                new Address() { Id = 3, City = "WR" }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetAddressById(It.IsAny<int>())).Returns((int id) => addressesInMemoryDatabase.SingleOrDefault(c => c.Id == id));
            var repository = mock.Object;

            var customerThatExists = repository.GetAddressById(7);
            customerThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnAddressByIdUserIdFromDb()
        {
            var addressesInMemoryDatabase = new List<Address>
            {
                new Address() { Id = 1, City = "NS", Customer = new Customer{ UserId="CGdg32" } },
                new Address() { Id = 2, City = "ZG", Customer = new Customer{ UserId="GD#GS" } },
                new Address() { Id = 3, City = "WR", Customer = new Customer{ UserId="@#sdg31G@" } }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetAddressById(It.IsAny<int>(), It.IsAny<string>())).Returns((int id, string userId) => addressesInMemoryDatabase.SingleOrDefault(c => c.Id == id && c.Customer.UserId == userId));
            var repository = mock.Object;

            var customerThatExists = repository.GetAddressById(2, "GD#GS");
            customerThatExists.Should().NotBeNull();
            customerThatExists.Should().BeSameAs(addressesInMemoryDatabase[1]);
            customerThatExists.Should().Be(addressesInMemoryDatabase[1]);
            customerThatExists.Should().BeOfType(typeof(Address));
        }

        [Fact]
        public void CantReturnAddressByIdUserIdFromDb()
        {
            var addressesInMemoryDatabase = new List<Address>
            {
                new Address() { Id = 1, City = "NS", Customer = new Customer{ UserId = "CGdg32" } },
                new Address() { Id = 2, City = "ZG", Customer = new Customer{ UserId = "GD#GS" } },
                new Address() { Id = 3, City = "WR", Customer = new Customer{ UserId = "@#sdg31G@" } }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetAddressById(It.IsAny<int>(), It.IsAny<string>())).Returns((int id, string userId) => addressesInMemoryDatabase.SingleOrDefault(c => c.Id == id && c.Customer.UserId == userId));
            var repository = mock.Object;

            var customerThatExists = repository.GetAddressById(2, "CGdg32");
            customerThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnContactDetailByIdFromDb()
        {
            var contactDetailsInMemoryDatabase = new List<ContactDetail>
            {
                new ContactDetail() { Id = 1, ContactDetailInformation = "235235321", ContactDetailTypeId = 1, CustomerId = 1 },
                new ContactDetail() { Id = 2, ContactDetailInformation = "klafj@sagk.pl", ContactDetailTypeId = 2, CustomerId = 2 },
                new ContactDetail() { Id = 3, ContactDetailInformation = "758469246", ContactDetailTypeId = 1, CustomerId = 3 }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetContactDetailById(It.IsAny<int>())).Returns((int id) => contactDetailsInMemoryDatabase.SingleOrDefault(c => c.Id == id));
            var repository = mock.Object;

            var contactDetailThatExists = repository.GetContactDetailById(2);
            contactDetailThatExists.Should().NotBeNull();
            contactDetailThatExists.Should().BeSameAs(contactDetailsInMemoryDatabase[1]);
            contactDetailThatExists.Should().Be(contactDetailsInMemoryDatabase[1]);
            contactDetailThatExists.Should().BeOfType(typeof(ContactDetail));
        }

        [Fact]
        public void CantReturnContactDetailByIdFromDb()
        {
            var contactDetailsInMemoryDatabase = new List<ContactDetail>
            {
                new ContactDetail() { Id = 1, ContactDetailInformation = "235235321", ContactDetailTypeId = 1, CustomerId = 1 },
                new ContactDetail() { Id = 2, ContactDetailInformation = "klafj@sagk.pl", ContactDetailTypeId = 2, CustomerId = 2 },
                new ContactDetail() { Id = 3, ContactDetailInformation = "758469246", ContactDetailTypeId = 1, CustomerId = 3 }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetContactDetailById(It.IsAny<int>())).Returns((int id) => contactDetailsInMemoryDatabase.SingleOrDefault(c => c.Id == id));
            var repository = mock.Object;

            var contactDetailThatExists = repository.GetContactDetailById(7);
            contactDetailThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnContactDetailByIdUserIdFromDb()
        {
            var contactDetailsInMemoryDatabase = new List<ContactDetail>
            {
                new ContactDetail() { Id = 1, ContactDetailInformation = "235235321", ContactDetailTypeId = 1, CustomerId = 1, Customer = new Customer{ UserId="CGdg32" } },
                new ContactDetail() { Id = 2, ContactDetailInformation = "klafj@sagk.pl", ContactDetailTypeId = 2, CustomerId = 2, Customer = new Customer{ UserId="GE@g2" } },
                new ContactDetail() { Id = 3, ContactDetailInformation = "758469246", ContactDetailTypeId = 1, CustomerId = 3, Customer = new Customer{ UserId="GDs#1" } }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetContactDetailById(It.IsAny<int>())).Returns((int id) => contactDetailsInMemoryDatabase.SingleOrDefault(c => c.Id == id));
            var repository = mock.Object;

            var contactDetailThatExists = repository.GetContactDetailById(2);
            contactDetailThatExists.Should().NotBeNull();
            contactDetailThatExists.Should().BeSameAs(contactDetailsInMemoryDatabase[1]);
            contactDetailThatExists.Should().Be(contactDetailsInMemoryDatabase[1]);
            contactDetailThatExists.Should().BeOfType(typeof(ContactDetail));
        }

        [Fact]
        public void CantReturnContactDetailByIdUserIdFromDb()
        {
            var contactDetailsInMemoryDatabase = new List<ContactDetail>
            {
                new ContactDetail() { Id = 1, ContactDetailInformation = "235235321", ContactDetailTypeId = 1, CustomerId = 1 },
                new ContactDetail() { Id = 2, ContactDetailInformation = "klafj@sagk.pl", ContactDetailTypeId = 2, CustomerId = 2 },
                new ContactDetail() { Id = 3, ContactDetailInformation = "758469246", ContactDetailTypeId = 1, CustomerId = 3 }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetContactDetailById(It.IsAny<int>(), It.IsAny<string>())).Returns((int id, string userId) => contactDetailsInMemoryDatabase.SingleOrDefault(c => c.Id == id && c.Customer.UserId == userId));
            var repository = mock.Object;

            var contactDetailThatExists = repository.GetContactDetailById(7);
            contactDetailThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnContactDetailTypeByIdFromDb()
        {
            var contactDetailTypesInMemoryDatabase = new List<ContactDetailType>
            {
                new ContactDetailType() { Id = 1, Name = "Fax" },
                new ContactDetailType() { Id = 2, Name = "Phone" },
                new ContactDetailType() { Id = 3, Name = "Email" }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetContactDetailTypeById(It.IsAny<int>())).Returns((int id) => contactDetailTypesInMemoryDatabase.SingleOrDefault(c => c.Id == id));
            var repository = mock.Object;

            var contactDetailTypeThatExists = repository.GetContactDetailTypeById(3);
            contactDetailTypeThatExists.Should().NotBeNull();
            contactDetailTypeThatExists.Should().BeSameAs(contactDetailTypesInMemoryDatabase[2]);
            contactDetailTypeThatExists.Should().Be(contactDetailTypesInMemoryDatabase[2]);
            contactDetailTypeThatExists.Should().BeOfType(typeof(ContactDetailType));
        }

        [Fact]
        public void CantReturnContactDetailTypeByIdFromDb()
        {
            var contactDetailTypesInMemoryDatabase = new List<ContactDetailType>
            {
                new ContactDetailType() { Id = 1, Name = "Fax" },
                new ContactDetailType() { Id = 2, Name = "Phone" },
                new ContactDetailType() { Id = 3, Name = "Email" }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetContactDetailTypeById(It.IsAny<int>())).Returns((int id) => contactDetailTypesInMemoryDatabase.SingleOrDefault(c => c.Id == id));
            var repository = mock.Object;

            var contactDetailTypeThatExists = repository.GetContactDetailTypeById(4);
            contactDetailTypeThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnContactDetailTypesFromDb()
        {
            var contactDetailTypesInMemoryDatabase = new List<ContactDetailType>
            {
                new ContactDetailType() { Id = 1, Name = "Fax" },
                new ContactDetailType() { Id = 2, Name = "Phone" },
                new ContactDetailType() { Id = 3, Name = "Email" }
            };

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetAllDetailTypes()).Returns(contactDetailTypesInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var contactDetailTypesThatExists = repository.GetAllDetailTypes();
            contactDetailTypesThatExists.Should().NotBeNull();
            contactDetailTypesThatExists.Should().HaveCount(3);
        }

        [Fact]
        public void CantReturnContactDetailTypesFromDb()
        {
            var contactDetailTypesInMemoryDatabase = new List<ContactDetailType>();

            var mock = new Mock<ICustomerRepository>();
            mock.Setup(x => x.GetAllDetailTypes()).Returns(contactDetailTypesInMemoryDatabase.AsQueryable());
            var repository = mock.Object;

            var contactDetailTypesThatExists = repository.GetAllDetailTypes();
            contactDetailTypesThatExists.Should().NotBeNull();
            contactDetailTypesThatExists.Should().HaveCount(0);
        }
    }
}
