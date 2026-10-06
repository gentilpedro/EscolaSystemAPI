using EscolaSystemApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaSystemApi.Infrastructure.Data.Configurations;

public class OrientadorClassConfiguration : IEntityTypeConfiguration<OrientadorClass>
{
    public void Configure(EntityTypeBuilder<OrientadorClass> builder)
    {
        builder.HasKey(x => new { x.OrientadorId, x.ClassId });
        // Vínculo encerrado continua no banco (histórico), mas some de todas as consultas
        builder.HasQueryFilter(x => x.EndedAt == null);

        builder.HasOne(x => x.Orientador)
            .WithMany(x => x.OrientadorClasses)
            .HasForeignKey(x => x.OrientadorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Class)
            .WithMany(x => x.OrientadorClasses)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
