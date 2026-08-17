using ECommerceApp.Domain.Sagas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sagas.Configurations
{
    internal sealed class SagaInstanceConfiguration : IEntityTypeConfiguration<SagaInstance>
    {
        public void Configure(EntityTypeBuilder<SagaInstance> builder)
        {
            builder.ToTable("SagaInstances");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedOnAdd();
            builder.Property(s => s.SagaType).HasMaxLength(200).IsRequired();
            builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(s => s.CorrelationId).HasMaxLength(450).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired();
            builder.HasIndex(s => new { s.SagaType, s.CorrelationId });
        }
    }
}