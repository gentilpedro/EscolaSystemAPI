using EscolaSystemApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaSystemApi.Infrastructure.Data.Configurations;

public class DisciplinaryCallConfiguration : IEntityTypeConfiguration<DisciplinaryCall>
{
    public void Configure(EntityTypeBuilder<DisciplinaryCall> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.Resolution).HasMaxLength(1000);

        builder.HasOne(x => x.Student)
            .WithMany(x => x.DisciplinaryCalls)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CreatedBy)
            .WithMany()
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ResolvedBy)
            .WithMany()
            .HasForeignKey(x => x.ResolvedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
