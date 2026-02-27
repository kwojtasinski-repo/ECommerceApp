using ECommerceApp.Domain.Supporting.TimeManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement.Configurations
{
    internal sealed class DeferredJobInstanceConfiguration : IEntityTypeConfiguration<DeferredJobInstance>
    {
        public void Configure(EntityTypeBuilder<DeferredJobInstance> builder)
        {
            builder.ToTable("DeferredJobQueue");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                   .HasConversion(x => x.Value, v => new DeferredJobInstanceId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(x => x.JobName)
                   .HasMaxLength(100)
                   .IsRequired();

            builder.Property(x => x.EntityId)
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(x => x.RunAt)
                   .IsRequired();

            builder.Property(x => x.Status)
                   .HasColumnType("tinyint")
                   .IsRequired();

            builder.Property(x => x.RetryCount)
                   .IsRequired()
                   .HasDefaultValue(0);

            builder.Property(x => x.MaxRetries)
                   .IsRequired()
                   .HasDefaultValue(3);

            builder.Property(x => x.LockExpiresAt);

            builder.Property(x => x.ErrorMessage);

            builder.Property(x => x.CreatedAt)
                   .IsRequired();
        }
    }
}
