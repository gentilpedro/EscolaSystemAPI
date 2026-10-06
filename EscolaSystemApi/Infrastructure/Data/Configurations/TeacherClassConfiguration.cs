using EscolaSystemApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaSystemApi.Infrastructure.Data.Configurations;

public class TeacherClassConfiguration : IEntityTypeConfiguration<TeacherClass>
{
    public void Configure(EntityTypeBuilder<TeacherClass> builder)
    {
        builder.HasKey(x => new { x.TeacherId, x.ClassId });
        // Vínculo encerrado continua no banco (histórico), mas some de todas as consultas
        builder.HasQueryFilter(x => x.EndedAt == null);

        builder.HasOne(x => x.Teacher)
            .WithMany(x => x.TeacherClasses)
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Class)
            .WithMany(x => x.TeacherClasses)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}