using EscolaSystemApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Infrastructure.Data;

public static class DbSeeder
{
    private static readonly Guid AdminId = new("00000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync(u => u.Email == "admin@escolasystem.com"))
            return;

        db.Users.Add(new User
        {
            Id = AdminId,
            Name = "Administrador",
            Email = "admin@escolasystem.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            RoleId = 1,
            IsActive = true
        });

        await db.SaveChangesAsync();
    }
}
