using AutoMapper;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Infrastructure;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Tests.Common
{
    public class ImplementationUtil
    {
        // zwraca instancje serwisu na podstawie typu podanego w argumencie
        public static IAbstractService<T, U, E> GetServiceInstance<T, U, R, E>(Type serviceType, Context context)
        {
            if (!typeof(IAbstractService<T, U, E>).IsAssignableFrom(serviceType))
            {
                throw new ArgumentException($"Passed wrong type of service. {serviceType} not implement IAbstractService");
            }

            var constructors = serviceType.GetConstructors();
            var paramInstances = new List<object>();
            System.Reflection.ConstructorInfo constructor;

            if (constructors.Length == 1)
            {
                constructor = constructors.FirstOrDefault();

                var parameters = constructor.GetParameters();

                foreach(var param in parameters)
                {
                    if (typeof(IMapper).IsAssignableFrom(param.ParameterType))
                    {
                        var mapper = GetMapperInstance();
                        paramInstances.Add(mapper);
                    }

                    if (typeof(IGenericRepository<E>).IsAssignableFrom(param.ParameterType))
                    {
                        var repo = (U) GetRepository<U, E>(typeof(R), context);
                        paramInstances.Add(repo);
                    }

                    if (typeof(IFileStore).IsAssignableFrom(param.ParameterType))
                    {
                        var fileStore = GetFileStoreInstance();
                        paramInstances.Add(fileStore);
                    }
                }
            }
            else
            {
                throw new NotImplementedException("Service shouldnt have more than one constructor, check your implementation or add another method which can handle more than one constructor");
            }

            if (paramInstances.Count == 0)
            {
                throw new InvalidCastException("There is no constructor for this kind of service, check your implementation");
            }

            var instance = (IAbstractService<T, U, E>) constructor.Invoke(paramInstances.ToArray());

            return instance;
        }

        // zwraca instancje mappera
        public static IMapper GetMapperInstance()
        {
            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            var mapper = configurationProvider.CreateMapper();

            return mapper;
        }

        // zwraca instancje repo w zaleznosci od typu
        public static IGenericRepository<E> GetRepository<U, E>(Type repositoryType, Context context)
        {
            if (!typeof(IGenericRepository<E>).IsAssignableFrom(repositoryType))
            {
                throw new ArgumentException($"Passed wrong type of repository. {repositoryType} not implement IGenericRepository");
            }

            if (!typeof(U).IsAssignableFrom(repositoryType))
            {
                throw new ArgumentException($"Passed wrong type of repository. {repositoryType} not implement {typeof(U).Name}");
            }

            var constructors = repositoryType.GetConstructors();
            var paramInstances = new List<object>();
            System.Reflection.ConstructorInfo constructor = null;

            if (constructors.Length == 1)
            {
                constructor = constructors.FirstOrDefault();

                var parameters = constructor.GetParameters();

                foreach (var param in parameters)
                {
                    if (param.ParameterType.IsAssignableFrom(typeof(Context)))
                    {
                        paramInstances.Add(context);
                    }
                }
            }
            else
            {
                throw new NotImplementedException("Repository shouldnt have more than one constructor, check your implementation or add another method which can handle more than one constructor");
            }

            if (paramInstances.Count == 0)
            {
                throw new InvalidCastException("There is no constructor for this kind of repository, check your implementation");
            }

            var instance = (IGenericRepository<E>)constructor.Invoke(paramInstances.ToArray());

            return instance;
        }

        // zwraca mock IFileStore
        public static IFileStore GetFileStoreInstance()
        {
            var mock = new Mock<IFileStore>();

            mock.Setup(s => s.ReadFile(It.IsAny<string>())).Returns(new byte[0]);
            mock.Setup(s => s.WriteFile(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), It.IsAny<string>()))
                .Returns(new Application.POCO.FileDirectoryPOCO { Name = Guid.NewGuid().ToString(), SourcePath = Guid.NewGuid().ToString() });
            mock.Setup(s => s.WriteFiles(It.IsAny<ICollection<Microsoft.AspNetCore.Http.IFormFile>>(), It.IsAny<string>()))
                .Returns(new List<Application.POCO.FileDirectoryPOCO>()
                { new Application.POCO.FileDirectoryPOCO { Name = Guid.NewGuid().ToString(), SourcePath = Guid.NewGuid().ToString() },
                new Application.POCO.FileDirectoryPOCO { Name = Guid.NewGuid().ToString(), SourcePath = Guid.NewGuid().ToString() }});
            mock.Setup(s => s.DeleteFile(It.IsAny<string>())).Verifiable();

           // var dirWrapper = GetDirectoryWrapper();
           // var fileWrapper = GetFileWrapper();
           //  var fileStore = new FileStore(fileWrapper, dirWrapper);
            return mock.Object;
        }

        // zwraca instancje IFileWrapper
        public static IFileWrapper GetFileWrapper()
        {
            var fileWrapper = new FileWrapper();
            return fileWrapper;
        }

        // zwraca instancje IDirectoryWrapper
        public static IDirectoryWrapper GetDirectoryWrapper()
        {
            var directoryWrapper = new DirectoryWrapper();
            return directoryWrapper;
        }
    }
}
