using System;
using System.Reflection;

namespace ECommerceApp.UnitTests
{
    internal static class EntityIdSetter
    {
        public static void Set<TEntity, TId>(TEntity entity, TId id)
        {
            var property = typeof(TEntity).GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.SetMethod is null)
            {
                throw new InvalidOperationException($"{typeof(TEntity).Name} must expose a writable Id property for unit tests.");
            }

            property.SetValue(entity, id);
        }
    }
}
