using EscolaSystemApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaSystemApi.Infrastructure.Data.Configurations;

public class SchoolMembershipConfiguration : IEntityTypeConfiguration<SchoolMembership>
{
    public void Configure(EntityTypeBuilder<SchoolMembership> builder)
    {
        builder.HasKey(x => x.Id);
        // Um vínculo ativo por pessoa e escola; os encerrados ficam como histórico
        builder.HasIndex(x => new { x.UserId, x.SchoolId });
        builder.HasIndex(x => new { x.SchoolId, x.EndedAt });

        builder.HasOne(x => x.User)
            .WithMany(x => x.SchoolMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
