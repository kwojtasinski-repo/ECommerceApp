using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement.Configurations
{
    internal sealed class ScheduledJobConfiguration : IEntityTypeConfiguration<ScheduledJob>
    {
        public void Configure(EntityTypeBuilder<ScheduledJob> builder)
        {
            builder.ToTable("ScheduledJobs");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                   .HasConversion(x => x.Value, v => new ScheduledJobId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(x => x.Name)
                   .HasConversion(x => x.Value, v => new JobName(v))
                   .HasMaxLength(100)
                   .IsRequired();

            builder.HasIndex(x => x.Name).IsUnique();

            builder.Property(x => x.Schedule)
                   .HasMaxLength(100)
                   .IsRequired();

            builder.Property(x => x.TimeZoneId)
                   .HasMaxLength(100);

            builder.Property(x => x.IsEnabled)
                   .IsRequired()
                   .HasDefaultValue(true);

            builder.Property(x => x.MaxRetries)
                   .IsRequired()
                   .HasDefaultValue(3);

            builder.Property(x => x.LastRunAt);
            builder.Property(x => x.NextRunAt);
        }
    }
}
