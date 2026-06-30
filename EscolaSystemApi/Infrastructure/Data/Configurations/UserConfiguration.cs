using EscolaSystemApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EscolaSystemApi.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.HasOne(x => x.School)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Cpf).HasMaxLength(14);
        builder.Property(x => x.CpfHash).HasMaxLength(64);
        builder.Property(x => x.CpfEncrypted).HasMaxLength(512);
        builder.HasIndex(x => x.CpfHash).HasDatabaseName("IX_Users_CpfHash");
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.HasOne(x => x.Student)
            .WithOne(x => x.UserAccount)
            .HasForeignKey<User>(x => x.StudentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Senha padrão: Admin@123
        const string passwordHash = "$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi";

        builder.HasData(
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "Administrador",
                Email = "admin@escolasystem.com",
                PasswordHash = passwordHash,
                RoleId = 1,
                IsActive = true,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Name = "Professor Exemplo",
                Email = "professor@escolasystem.com",
                PasswordHash = passwordHash,
                RoleId = 2,
                IsActive = true,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Name = "Aluno Exemplo",
                Email = "aluno@escolasystem.com",
                PasswordHash = passwordHash,
                RoleId = 3,
                IsActive = true,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                Name = "Responsável Exemplo",
                Email = "responsavel@escolasystem.com",
                PasswordHash = passwordHash,
                RoleId = 4,
                IsActive = true,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}