using ECommerceApp.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.FileManager
{
    internal static class Extensions
    {
        public static IServiceCollection AddFilesStore(this IServiceCollection services)
        {
            services.AddTransient<IFileStore, FileStore>();
            services.AddTransient<IFileWrapper, FileWrapper>();
            services.AddTransient<IDirectoryWrapper, DirectoryWrapper>();
            return services;
        }
    }
}
