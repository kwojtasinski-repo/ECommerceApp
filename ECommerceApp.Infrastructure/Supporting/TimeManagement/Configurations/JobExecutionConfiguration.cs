using ECommerceApp.Domain.Supporting.TimeManagement;
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

            builder.Property(x => x.ScheduledJobId)
                   .HasConversion(x => x.Value, v => new ScheduledJobId(v))
                   .IsRequired();

            builder.HasOne<ScheduledJob>()
                   .WithMany()
                   .HasForeignKey(x => x.ScheduledJobId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.DeferredInstanceId)
                   .HasConversion(
                       x => x != null ? (int?)x.Value : null,
                       v => v.HasValue ? new DeferredJobInstanceId(v.Value) : null);

            builder.HasOne<DeferredJobInstance>()
                   .WithMany()
                   .HasForeignKey(x => x.DeferredInstanceId)
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.Source)
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
