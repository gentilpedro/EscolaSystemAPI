using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaSystemApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "TeacherClasses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "ParentStudents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "OrientadorClasses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SchoolMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolMemberships_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolMemberships_SchoolId_EndedAt",
                table: "SchoolMemberships",
                columns: new[] { "SchoolId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolMemberships_UserId_SchoolId",
                table: "SchoolMemberships",
                columns: new[] { "UserId", "SchoolId" });

            // Quem já tinha escola passa a ter o vínculo com ela, com a data de cadastro como entrada
            migrationBuilder.Sql("""
                INSERT INTO "SchoolMemberships" ("Id", "UserId", "SchoolId", "CreatedAt", "EndedAt")
                SELECT gen_random_uuid(), u."Id", u."SchoolId", u."CreatedAt", NULL
                FROM "Users" u
                WHERE u."SchoolId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolMemberships");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "TeacherClasses");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "ParentStudents");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "OrientadorClasses");
        }
    }
}
