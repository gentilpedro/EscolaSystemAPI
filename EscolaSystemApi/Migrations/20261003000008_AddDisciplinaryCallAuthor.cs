using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaSystemApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDisciplinaryCallAuthor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "DisciplinaryCalls",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryCalls_CreatedById",
                table: "DisciplinaryCalls",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_DisciplinaryCalls_Users_CreatedById",
                table: "DisciplinaryCalls",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DisciplinaryCalls_Users_CreatedById",
                table: "DisciplinaryCalls");

            migrationBuilder.DropIndex(
                name: "IX_DisciplinaryCalls_CreatedById",
                table: "DisciplinaryCalls");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "DisciplinaryCalls");
        }
    }
}
