using EscolaSystemApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Infrastructure.Data;

public static class DbSeeder
{
    private static readonly Guid AdminId = new("00000000-0000-0000-0000-000000000001");

    // Hash fixo semeado pelas migrations (UserConfiguration.HasData). Não corresponde à senha
    // documentada, então nenhuma conta semeada conseguia logar num banco novo.
    public const string MigrationSeedHash = "$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi";

    public const string DefaultAdminPassword = "Admin@123";

    public static async Task SeedAsync(AppDbContext db)
    {
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@escolasystem.com");

        if (admin is null)
        {
            db.Users.Add(new User
            {
                Id = AdminId,
                Name = "Administrador",
                Email = "admin@escolasystem.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword),
                RoleId = RoleIds.Admin,
                IsActive = true
            });
        }
        else if (admin.PasswordHash == MigrationSeedHash)
        {
            // Só ajusta enquanto a senha nunca foi trocada
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword);
        }

        // Contas de exemplo das migrations: perfis inconsistentes e sem escola. Ficam inativas
        // enquanto ninguém tiver definido uma senha real para elas.
        var exampleAccounts = await db.Users
            .Where(u => u.Id != AdminId && u.PasswordHash == MigrationSeedHash && u.IsActive)
            .ToListAsync();

        foreach (var account in exampleAccounts)
            account.IsActive = false;

        await db.SaveChangesAsync();
    }
}
