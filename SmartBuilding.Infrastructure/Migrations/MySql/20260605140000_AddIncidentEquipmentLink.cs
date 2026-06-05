using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartBuilding.Infrastructure.Persistence;

#nullable disable

namespace SmartBuilding.Infrastructure.Migrations.MySql;

[DbContext(typeof(SmartBuildingDbContext))]
[Migration("20260605140000_AddIncidentEquipmentLink")]
/// <inheritdoc />
public partial class AddIncidentEquipmentLink : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "EquipmentId",
            table: "Incidents",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.CreateIndex(
            name: "IX_Incidents_EquipmentId",
            table: "Incidents",
            column: "EquipmentId");

        migrationBuilder.AddForeignKey(
            name: "FK_Incidents_Equipment_EquipmentId",
            table: "Incidents",
            column: "EquipmentId",
            principalTable: "Equipment",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Incidents_Equipment_EquipmentId",
            table: "Incidents");

        migrationBuilder.DropIndex(
            name: "IX_Incidents_EquipmentId",
            table: "Incidents");

        migrationBuilder.DropColumn(
            name: "EquipmentId",
            table: "Incidents");
    }
}
