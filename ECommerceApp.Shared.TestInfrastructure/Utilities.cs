using ECommerceApp.Domain.Identity.IAM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MimeTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ECommerceApp.Shared.TestInfrastructure
{
    public class Utilities
    {
        public static async Task InitializeIamUsers(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            // Ensure roles exist
            foreach (var role in new[] { "Administrator", "Manager", "Service", "User", "NotRegister" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Create test user
            var testUser = new ApplicationUser
            {
                Id = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e",
                UserName = "test@test",
                Email = "test@test",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(testUser, "Test@test12");
            await userManager.AddToRoleAsync(testUser, "Administrator");

            // Create second test user
            var testUser2 = new ApplicationUser
            {
                Id = "e4fc1feb-7d08-4207-bd52-3f3464a01564",
                UserName = "test2@test2",
                Email = "test2@test2",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(testUser2, "Test@test12");
        }

        public static async Task<IFormFile> CreateIFormFileFrom(string filePath)
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
            System.Type type = typeof(T);

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

