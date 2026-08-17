using ECommerceApp.Domain.Sagas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sagas.Configurations
{
    internal sealed class SagaStepConfiguration : IEntityTypeConfiguration<SagaStep>
    {
        public void Configure(EntityTypeBuilder<SagaStep> builder)
        {
            builder.ToTable("SagaSteps");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedOnAdd();
            builder.Property(s => s.SagaInstanceId).IsRequired();
            builder.Property(s => s.StepName).HasMaxLength(200).IsRequired();
            builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(s => s.OccurredAt).IsRequired();
            builder.Property(s => s.Payload).IsRequired();

            builder.HasOne<SagaInstance>()
                .WithMany()
                .HasForeignKey(s => s.SagaInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(s => new { s.SagaInstanceId, s.StepName });
        }
    }
}