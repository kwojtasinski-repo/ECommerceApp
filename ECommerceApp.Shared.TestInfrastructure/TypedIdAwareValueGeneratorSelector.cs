using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.InMemory.ValueGeneration.Internal;
using Microsoft.EntityFrameworkCore.InMemory.Storage.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System;

namespace ECommerceApp.Shared.TestInfrastructure
{
    /// <summary>
    /// Extends the built-in <see cref="InMemoryValueGeneratorSelector"/> to support
    /// typed-ID value objects (e.g. <c>PaymentId</c>, <c>CurrencyId</c>) that wrap an <c>int</c>.
    ///
    /// <para>
    /// The InMemory provider can auto-generate <c>int</c>/<c>long</c>/<c>Guid</c> primary keys
    /// but fails for custom types configured with <c>ValueGeneratedOnAdd()</c>.
    /// This selector detects typed IDs by looking for a single-<c>int</c> constructor and
    /// a <c>Value</c> property, then delegates to <see cref="TypedIdValueGenerator"/>.
    /// </para>
    /// </summary>
#pragma warning disable EF1001 // Internal EF Core API usage (InMemoryValueGeneratorSelector)
    public sealed class TypedIdAwareValueGeneratorSelector : InMemoryValueGeneratorSelector
    {
        public TypedIdAwareValueGeneratorSelector(
            ValueGeneratorSelectorDependencies dependencies,
            IInMemoryDatabase inMemoryDatabase)
            : base(dependencies, inMemoryDatabase) { }

        [Obsolete]
        public override ValueGenerator Create(IProperty property, ITypeBase entityType)
        {
            var clrType = property.ClrType;

            if (clrType != typeof(int)
                && clrType != typeof(long)
                && clrType != typeof(System.Guid)
                && clrType != typeof(string))
            {
                var valueProperty = clrType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                var constructor = clrType.GetConstructor(new[] { typeof(int) });

                if (valueProperty != null
                    && valueProperty.PropertyType == typeof(int)
                    && constructor != null)
                {
                    return TypedIdValueGenerator.GetOrCreate(clrType, constructor);
                }
            }

            return base.Create(property, entityType);
        }
    }
#pragma warning restore EF1001

    /// <summary>
    /// Generates incrementing <c>int</c> values wrapped in a typed-ID constructor.
    /// Each typed-ID CLR type gets its own monotonically increasing counter.
    /// </summary>
    public sealed class TypedIdValueGenerator : ValueGenerator
    {
        private static readonly ConcurrentDictionary<System.Type, TypedIdValueGenerator> _generators = new();

        private readonly ConstructorInfo _constructor;
        private int _counter;

        private TypedIdValueGenerator(ConstructorInfo constructor)
        {
            _constructor = constructor;
        }

        public static TypedIdValueGenerator GetOrCreate(System.Type typedIdType, ConstructorInfo constructor)
            => _generators.GetOrAdd(typedIdType, _ => new TypedIdValueGenerator(constructor));

        public override bool GeneratesTemporaryValues => false;

        protected override object NextValue(EntityEntry entry)
            => _constructor.Invoke(new object[] { Interlocked.Increment(ref _counter) });
    }
}

