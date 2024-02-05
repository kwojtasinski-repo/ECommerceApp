using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal static class PrimaryKeyGenerationStrategy
    {
        public static ModelBuilder ApplyPrimaryKeyGeneration(this ModelBuilder modelBuilder)
        {
            var keysProperties = modelBuilder.Model.GetEntityTypes().Select(x => x.FindPrimaryKey()).SelectMany(x => x.Properties);
            foreach (var property in keysProperties)
            {
                property.ValueGenerated = ValueGenerated.OnAdd;
            }
            return modelBuilder;
        }
    }
}
