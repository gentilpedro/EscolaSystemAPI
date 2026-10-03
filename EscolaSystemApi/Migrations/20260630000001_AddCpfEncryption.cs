using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaSystemApi.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260630000001_AddCpfEncryption")]
public partial class AddCpfEncryption : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CpfEncrypted",
            table: "Users",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CpfHash",
            table: "Users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_CpfHash",
            table: "Users",
            column: "CpfHash");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Users_CpfHash",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "CpfEncrypted",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "CpfHash",
            table: "Users");
    }
}
