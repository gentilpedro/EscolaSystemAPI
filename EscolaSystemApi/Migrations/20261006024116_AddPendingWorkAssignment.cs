using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscolaSystemApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingWorkAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentId",
                table: "PendingWorks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Registros que já existiam: os que têm a mesma turma, título, descrição e prazo foram lançados juntos
            // e passam a formar um trabalho só (gen_random_uuid é nativo do PostgreSQL 13+)
            migrationBuilder.Sql("""
                WITH grupos AS (
                    SELECT "ClassId", "Title", "Description", "DueDate", gen_random_uuid() AS "NovoId"
                    FROM "PendingWorks"
                    GROUP BY "ClassId", "Title", "Description", "DueDate"
                )
                UPDATE "PendingWorks" p
                SET "AssignmentId" = g."NovoId"
                FROM grupos g
                WHERE p."ClassId" = g."ClassId"
                  AND p."Title" = g."Title"
                  AND p."Description" = g."Description"
                  AND p."DueDate" = g."DueDate";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PendingWorks_AssignmentId",
                table: "PendingWorks",
                column: "AssignmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingWorks_AssignmentId",
                table: "PendingWorks");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "PendingWorks");
        }
    }
}
