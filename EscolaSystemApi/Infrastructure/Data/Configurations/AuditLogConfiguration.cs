using EscolaSystemApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaSystemApi.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TargetType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TargetName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Details).HasMaxLength(2000);

        // Consultas: mais recentes primeiro, por pessoa e por alvo
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.ActorId);
        builder.HasIndex(x => x.TargetId);
    }
}
