using EscolaSystemApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaSystemApi.Infrastructure.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(300);
        builder.HasIndex(x => x.Name).IsUnique();


        builder.HasData(
            new Role { Id = 1, Name = "Admin", Description = "Administrador do sistema" },
            new Role { Id = 2, Name = "Director", Description = "Diretor da escola" },
            new Role { Id = 3, Name = "Teacher", Description = "Professor" },
            new Role { Id = 4, Name = "Student", Description = "Aluno" },
            new Role { Id = 5, Name = "Parent", Description = "Responsável" },
            new Role { Id = 6, Name = "Orientador", Description = "Orientador pedagógico" }
        );
    }
}