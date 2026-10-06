using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable


namespace EscolaSystemApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveExampleAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Contas de exemplo antigas (perfis trocados). Só saem se ainda têm a senha de fábrica e nada
            // ligado a elas; as que alguém chegou a usar ficam (o DbSeeder as mantém desativadas).
            migrationBuilder.Sql("""
                DELETE FROM "Users" u
                WHERE u."Id" IN ('00000000-0000-0000-0000-000000000002',
                                 '00000000-0000-0000-0000-000000000003',
                                 '00000000-0000-0000-0000-000000000004')
                  AND u."PasswordHash" = '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi'
                  AND NOT EXISTS (SELECT 1 FROM "DisciplinaryCalls" d WHERE d."CreatedById" = u."Id" OR d."ResolvedById" = u."Id")
                  AND NOT EXISTS (SELECT 1 FROM "TeacherClasses" t WHERE t."TeacherId" = u."Id")
                  AND NOT EXISTS (SELECT 1 FROM "OrientadorClasses" o WHERE o."OrientadorId" = u."Id")
                  AND NOT EXISTS (SELECT 1 FROM "ParentStudents" p WHERE p."ParentId" = u."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volta as contas de exemplo, inativas, só onde não existem mais
            migrationBuilder.Sql("""
                INSERT INTO "Users" ("Id", "CreatedAt", "Email", "IsActive", "Name", "PasswordHash", "RoleId")
                VALUES
                  ('00000000-0000-0000-0000-000000000002', '2025-01-01T00:00:00Z', 'professor@escolasystem.com', FALSE, 'Professor Exemplo', '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 2),
                  ('00000000-0000-0000-0000-000000000003', '2025-01-01T00:00:00Z', 'aluno@escolasystem.com', FALSE, 'Aluno Exemplo', '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 3),
                  ('00000000-0000-0000-0000-000000000004', '2025-01-01T00:00:00Z', 'responsavel@escolasystem.com', FALSE, 'Responsável Exemplo', '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 4)
                ON CONFLICT ("Id") DO NOTHING;
                """);
        }
    }
}
