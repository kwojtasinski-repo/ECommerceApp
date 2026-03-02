using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement.Configurations
{
    internal sealed class JobExecutionConfiguration : IEntityTypeConfiguration<JobExecution>
    {
        public void Configure(EntityTypeBuilder<JobExecution> builder)
        {
            builder.ToTable("JobExecutions");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                   .HasConversion(x => x.Value, v => new JobExecutionId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(x => x.JobName)
                   .HasConversion(x => x.Value, v => new JobName(v))
                   .HasMaxLength(100)
                   .IsRequired();

            builder.Property(x => x.DeferredQueueId)
                   .HasConversion(
                       id => id != null ? (int?)id.Value : null,
                       val => val.HasValue ? new DeferredJobInstanceId(val.Value) : null);

            builder.Property(x => x.Source)
                   .HasConversion<byte>()
                   .HasColumnType("tinyint")
                   .IsRequired();

            builder.Property(x => x.ExecutionId)
                   .HasMaxLength(36)
                   .IsRequired();

            builder.Property(x => x.StartedAt)
                   .IsRequired();

            builder.Property(x => x.CompletedAt);

            builder.Property(x => x.Succeeded)
                   .IsRequired();

            builder.Property(x => x.Message);
        }
    }
}
