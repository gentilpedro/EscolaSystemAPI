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
        builder.Property(x => x.FailedLoginAttempts).HasDefaultValue(0);
        builder.HasOne(x => x.Student)
            .WithOne(x => x.UserAccount)
            .HasForeignKey<User>(x => x.StudentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Hash da senha de fábrica do admin (documentada no README, só para desenvolvimento).
        // As contas de exemplo antigas (professor@, aluno@, responsavel@escolasystem.com) saíram: tinham perfis trocados.
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
            }
        );
    }
}