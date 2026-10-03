using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class DbSeederTests
{
    [Fact]
    public async Task SeedAsync_AdminWithMigrationHash_GetsDocumentedPassword()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User
        {
            Id = new Guid("00000000-0000-0000-0000-000000000001"),
            Name = "Administrador",
            Email = "admin@escolasystem.com",
            PasswordHash = DbSeeder.MigrationSeedHash,
            RoleId = RoleIds.Admin,
            IsActive = true
        });
        context.SaveChanges();

        await DbSeeder.SeedAsync(context);

        var admin = context.Users.Single(u => u.Email == "admin@escolasystem.com");
        BCrypt.Net.BCrypt.Verify(DbSeeder.DefaultAdminPassword, admin.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task SeedAsync_AdminWithChangedPassword_IsNotTouched()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var customHash = BCrypt.Net.BCrypt.HashPassword("Minha@Senha1");
        context.Users.Add(new User
        {
            Name = "Administrador",
            Email = "admin@escolasystem.com",
            PasswordHash = customHash,
            RoleId = RoleIds.Admin,
            IsActive = true
        });
        context.SaveChanges();

        await DbSeeder.SeedAsync(context);

        context.Users.Single(u => u.Email == "admin@escolasystem.com").PasswordHash.Should().Be(customHash);
    }

    [Fact]
    public async Task SeedAsync_ExampleAccountsWithMigrationHash_AreDeactivated()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User
        {
            Name = "Professor Exemplo",
            Email = "professor@escolasystem.com",
            PasswordHash = DbSeeder.MigrationSeedHash,
            RoleId = RoleIds.Director,
            IsActive = true
        });
        context.SaveChanges();

        await DbSeeder.SeedAsync(context);

        context.Users.Single(u => u.Email == "professor@escolasystem.com").IsActive.Should().BeFalse();
        context.Users.Should().Contain(u => u.Email == "admin@escolasystem.com" && u.IsActive);
    }
}
