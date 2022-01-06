using ECommerceApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using MimeTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceApp.IntegrationTests.Common
{
    internal class Utilities
    {
        public static void InitilizeDbForTests(Context context)
        {
            // ---------------------------------- Dane testowe ----------------------------------

            var address = new Domain.Model.Address { Id = 1, BuildingNumber = "2", FlatNumber = 10, City = "Nowa Sól", Country = "Poland", Street = "Testowa" , CustomerId = 1, ZipCode = 67100 };
            context.Add(address);

            var contactDetail = new Domain.Model.ContactDetail() { Id = 1, ContactDetailInformation = "867123563", ContactDetailTypeId = 1, CustomerId = 1 };
            context.Add(contactDetail);

            var customer = new Domain.Model.Customer { Id = 1, FirstName = "Mr", LastName = "Tester", IsCompany = false, UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e", Addresses = new List<Domain.Model.Address>(), ContactDetails = new List<Domain.Model.ContactDetail>() };
            customer.Addresses.Add(address);
            customer.ContactDetails.Add(contactDetail);
            context.Add(customer);

            var brand = new Domain.Model.Brand() { Id = 1, Name = "Samsung" };
            context.Add(brand);
            var brand2 = new Domain.Model.Brand() { Id = 2, Name = "Goplana" };
            context.Add(brand2);
            var brand3 = new Domain.Model.Brand() { Id = 3, Name = "Xiaomi" };
            context.Add(brand3);

            var type = new Domain.Model.Type { Id = 1, Name = "TypeDelete" };
            context.Add(type);

            var contactDetailType = new Domain.Model.ContactDetailType { Id = 3, Name = "ContactDetailTypeDelete" };
            context.Add(contactDetailType);

            var currency1 = new Domain.Model.Currency { Id = 2, Code = "EUR", Description = "Euro" };
            context.Add(currency1);
            var currency2 = new Domain.Model.Currency { Id = 3, Code = "CHF", Description = "Frank szwajcarski" };
            context.Add(currency2);
            var currency3 = new Domain.Model.Currency { Id = 4, Code = "USD", Description = "Dolar amerykański" };
            context.Add(currency3);
            var currency4 = new Domain.Model.Currency { Id = 5, Code = "ABC", Description = "Test Test" };
            context.Add(currency4);

            var currencyRate1 = new Domain.Model.CurrencyRate { Id = 1, Currency = currency1, CurrencyDate = DateTime.Now.Date, CurrencyId = 1, Rate = 1 };
            context.Add(currencyRate1);
            var currencyRate2 = new Domain.Model.CurrencyRate { Id = 2, Currency = currency2, CurrencyDate = DateTime.Now.Date, CurrencyId = 2, Rate = new decimal(0.5214) };
            context.Add(currencyRate2);

            //user: { email: "test@test", userName: "test@test", password: "Test@test12" }
            var testUser = new IdentityUser 
            { 
                Id = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e", 
                Email = "test@test", 
                UserName = "test@test",
                NormalizedUserName = "TEST@TEST",
                PasswordHash = "AQAAAAEAACcQAAAAEAhNvr909GdhKMLVvTQ6kj17HAWZPg6c+YgQ8rl/m1Ww6Pf+fqJ8FUf+yU5N5stXOA==", ConcurrencyStamp = "db68806b-190f-4abf-a39f-9b2d74039dd9",
                SecurityStamp = string.Empty,
                EmailConfirmed = true
            };
            context.Add(testUser);
            
            var userRole = new IdentityUserRole<string>
            {
                RoleId = "Administrator",
                UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e"
            };
            context.Add(userRole);

            //user: { email: "test2@test2", userName: "test2@test2", password: "Test@test12" }
            var testUser2 = new IdentityUser
            {
                Id = "e4fc1feb-7d08-4207-bd52-3f3464a01564",
                Email = "test2@test2",
                UserName = "test2@test2",
                NormalizedUserName = "TEST2@TEST2",
                PasswordHash = "AQAAAAEAACcQAAAAEAhNvr909GdhKMLVvTQ6kj17HAWZPg6c+YgQ8rl/m1Ww6Pf+fqJ8FUf+yU5N5stXOA==",
                SecurityStamp = string.Empty,
                EmailConfirmed = true
            };
            context.Add(testUser2);

            var couponType = new Domain.Model.CouponType { Id = 1, Type = "Type1" };
            context.Add(couponType);

            var coupon = new Domain.Model.Coupon { Id = 1, Code = "AGEWEDSGFEW", CouponTypeId = 1, Description = "DesciprtionText", Discount = 10 };
            context.Add(coupon);

            var image = new Domain.Model.Image { Id = 1, ItemId = 1, Name = "image1", SourcePath = "../src/image1.jpg" };
            context.Add(image);

            var item = new Domain.Model.Item { Id = 1, BrandId = 1, Cost = new decimal(2500), CurrencyId = 1, Description = "ItemTestDescription", Name = "Samsung New", Quantity = 50, Warranty = "365", TypeId = 1 };
            item.Brand = brand;
            item.Type = type;
            context.Add(item);
            var tag2 = new Domain.Model.Tag { Id = 2, Name = "Tag2" };
            context.Add(tag2);
            var itemTag = new Domain.Model.ItemTag { ItemId = 1, TagId = 2 };
            context.Add(itemTag);

            var item2 = new Domain.Model.Item { Id = 2, BrandId = 1, Cost = new decimal(2500), CurrencyId = 1, Description = "ItemTestDescription", Name = "Item2", Quantity = 50, Warranty = "365", TypeId = 1, Brand = brand, Type = type };
            var item3 = new Domain.Model.Item { Id = 3, BrandId = 1, Cost = new decimal(2500), CurrencyId = 1, Description = "ItemTestDescription", Name = "Item3", Quantity = 50, Warranty = "365", TypeId = 1, Brand = brand, Type = type };
            var item4 = new Domain.Model.Item { Id = 4, BrandId = 1, Cost = new decimal(2500), CurrencyId = 1, Description = "ItemTestDescription", Name = "Item4", Quantity = 50, Warranty = "365", TypeId = 1, Brand = brand, Type = type };
            context.Add(item2);
            context.Add(item3);
            context.Add(item4);

            var orderItem = new Domain.Model.OrderItem { Id = 1, ItemId = 1, ItemOrderQuantity = 1, OrderId = 1, UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e" };
            context.Add(orderItem);
            var item5 = new Domain.Model.Item { Id = 5, BrandId = 1, Cost = new decimal(2500), CurrencyId = 1, Description = "ItemTestDescription", Name = "Nr5", Quantity = 50, Warranty = "365", TypeId = 1, Brand = brand, Type = type };
            context.Add(item5);
            var item6 = new Domain.Model.Item { Id = 6, BrandId = 1, Cost = new decimal(2500), CurrencyId = 1, Description = "ItemTestDescription", Name = "Nr6", Quantity = 50, Warranty = "365", TypeId = 1, Brand = brand, Type = type };
            context.Add(item6);
            var orderItem2 = new Domain.Model.OrderItem { Id = 2, ItemId = 5, ItemOrderQuantity = 1, UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e" };
            context.Add(orderItem2);
            var orderItem3 = new Domain.Model.OrderItem { Id = 3, ItemId = 6, ItemOrderQuantity = 1, UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e" };
            context.Add(orderItem3);

            var order = new Domain.Model.Order { Id = 1, Cost = new decimal(2500), CurrencyId = 1, CustomerId = 1, Number = 12445, UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e", Ordered = DateTime.Now };
            context.Add(order);
            var order2 = new Domain.Model.Order { Id = 2, Cost = new decimal(1000), CurrencyId = 1, CustomerId = 1, Number = 153465, UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e", Ordered = DateTime.Now, IsPaid = true, PaymentId = 1 };
            context.Add(order2);
            var order3 = new Domain.Model.Order { Id = 3, Cost = new decimal(1000), CurrencyId = 1, CustomerId = 1, Number = 153465, UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e", Ordered = DateTime.Now };
            context.Add(order3);

            var payment = new Domain.Model.Payment { Id = 1, CurrencyId = 1, CustomerId = 1, DateOfOrderPayment = DateTime.Now, OrderId = 2, Number = 12452 };
            context.Add(payment);

            var order4 = new Domain.Model.Order { Id = 4, Cost = new decimal(1000), CurrencyId = 1, CustomerId = 1, Number = 153465, UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e", Ordered = DateTime.Now, RefundId = 1, IsPaid = true, PaymentId = 2 };
            context.Add(order4);
            var payment2 = new Domain.Model.Payment { Id = 2, CurrencyId = 1, CustomerId = 1, DateOfOrderPayment = DateTime.Now, OrderId = 4, Number = 12452 };
            context.Add(payment2);
            var refund = new Domain.Model.Refund { Id = 1, CustomerId = 1, OnWarranty = true, OrderId = 4, Reason = "TestReason", RefundDate = DateTime.Now };
            context.Add(refund);

            var order5 = new Domain.Model.Order { Id = 5, Cost = new decimal(1000), CurrencyId = 1, CustomerId = 1, Number = 153465, UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e", Ordered = DateTime.Now, IsPaid = true, PaymentId = 3, IsDelivered = true };
            context.Add(order5);
            var item7 = new Domain.Model.Item { Id = 7, BrandId = 1, Cost = new decimal(1000), CurrencyId = 1, Description = "ItemTestDescriptionABC", Name = "Nr6", Quantity = 50, Warranty = "365", TypeId = 1, Brand = brand, Type = type };
            context.Add(item7);
            var orderItem4 = new Domain.Model.OrderItem { Id = 4, OrderId = 5, ItemId = 7, ItemOrderQuantity = 1, UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e" };
            context.Add(orderItem4);
            var payment3 = new Domain.Model.Payment { Id = 3, CurrencyId = 1, CustomerId = 1, DateOfOrderPayment = DateTime.Now, OrderId = 2, Number = 12452 };
            context.Add(payment3);

            var tag = new Domain.Model.Tag { Id = 1, Name = "Tag" };
            context.Add(tag);

            // ---------------------------------- Dane testowe ----------------------------------

            context.SaveChanges();
        }

        public static async Task<IFormFile> AddFileToIFormFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(fileName);
            var mimeType = MimeTypeMap.GetMimeType(extension);
            var bytes = await File.ReadAllBytesAsync(filePath);
            var stream = new MemoryStream(bytes);
            var formFile = new FormFile(stream, 0, stream.Length, extension, fileName)
            {
                Headers = new HeaderDictionary(),
                ContentDisposition = mimeType
            };
            return formFile;
        }

        public static MultipartFormDataContent SerializeObjectWithImageToBytes<T>(T obj)
        {
            Type type = typeof(T);

            // jesli przekazuje liste IFormFile
            if (typeof(System.Collections.Generic.ICollection<IFormFile>).IsAssignableFrom(type))
            {
                List<IFormFile> files = obj as List<IFormFile>;
                MultipartFormDataContent multiContent = FilesToMultiContent(files);
                return multiContent;
            }
            // jesli przekazuje IFormFile
            else if (typeof(IFormFile).IsAssignableFrom(type))
            {
                IFormFile file = obj as IFormFile;
                MultipartFormDataContent multiContent = FileToMultiContent(file);
                return multiContent;
            }
            // jesli przekazuje object zawierajacy IFormFile lub ICollection<IFormFile>
            else
            {
                var properties = type.GetProperties();

                var listIFormFile = properties.Where(o => typeof(System.Collections.Generic.ICollection<IFormFile>).IsAssignableFrom(o.PropertyType)).FirstOrDefault();
                var iFormFile = properties.Where(o => o.PropertyType.Equals(typeof(IFormFile))).FirstOrDefault();

                if (listIFormFile != null)
                {
                    List<IFormFile> files = (List<IFormFile>)listIFormFile.GetValue(obj);
                    MultipartFormDataContent multiContent = FilesToMultiContent(files);

                    var filterProperties = properties.Where(p => !typeof(System.Collections.Generic.ICollection<IFormFile>).IsAssignableFrom(p.PropertyType)).ToList();

                    foreach (var prop in filterProperties)
                    {
                        multiContent.Add(new StringContent(prop.GetValue(obj).ToString()), prop.Name);
                    }

                    return multiContent;
                }
                else if (iFormFile != null)
                {
                    IFormFile file = (IFormFile)iFormFile.GetValue(obj);
                    MultipartFormDataContent multiContent = FileToMultiContent(file);

                    var filterProperties = properties.Where(p => !p.PropertyType.Equals(typeof(IFormFile))).ToList();
                    foreach (var prop in filterProperties)
                    {
                        multiContent.Add(new StringContent(prop.GetValue(obj).ToString()), prop.Name);
                    }

                    return multiContent;
                }
                else
                {
                    throw new Exception("There is no IFormFile");
                }
            }
        }

        private static MultipartFormDataContent FilesToMultiContent(ICollection<IFormFile> formFiles)
        {
            MultipartFormDataContent multiContent = new MultipartFormDataContent();
            foreach (var file in formFiles)
            {
                var fileContent = new StreamContent(file.OpenReadStream());
                fileContent.Headers.Add("Content-Disposition", $"form-data; name=\"files\"; filename=\"{file.FileName}\"");
                multiContent.Add(fileContent);
            }

            return multiContent;
        }

        private static MultipartFormDataContent FileToMultiContent(IFormFile formFile)
        {
            MultipartFormDataContent multiContent = new MultipartFormDataContent();
            var fileContent = new StreamContent(formFile.OpenReadStream());
            fileContent.Headers.Add("Content-Disposition", $"form-data; name=\"file\"; filename=\"{formFile.FileName}\"");
            multiContent.Add(fileContent);
            return multiContent;
        }
    }
}
