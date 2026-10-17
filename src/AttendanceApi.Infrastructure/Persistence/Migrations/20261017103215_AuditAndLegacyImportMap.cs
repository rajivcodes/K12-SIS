using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditAndLegacyImportMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntry",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegacyIdMap",
                columns: table => new
                {
                    EntityType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NewId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyIdMap", x => new { x.EntityType, x.LegacyId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntry_EntityType_EntityId",
                table: "AuditEntry",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntry_OccurredAt",
                table: "AuditEntry",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyIdMap_EntityType_NewId",
                table: "LegacyIdMap",
                columns: new[] { "EntityType", "NewId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntry");

            migrationBuilder.DropTable(
                name: "LegacyIdMap");
        }
    }
}
