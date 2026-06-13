using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartBuilding.Infrastructure.Persistence;

#nullable disable

namespace SmartBuilding.Infrastructure.Migrations.MySql;

[DbContext(typeof(SmartBuildingDbContext))]
[Migration("20260605130000_AddConsumptionExpenseFields")]
/// <inheritdoc />
public partial class AddConsumptionExpenseFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ExpenseMotif",
            table: "ConsumptionRecords",
            type: "longtext",
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "PaidBy",
            table: "ConsumptionRecords",
            type: "longtext",
            nullable: false,
            defaultValue: "")
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "ReimbursementStatus",
            table: "ConsumptionRecords",
            type: "longtext",
            nullable: false,
            defaultValue: "Non applicable")
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ExpenseMotif", table: "ConsumptionRecords");
        migrationBuilder.DropColumn(name: "PaidBy", table: "ConsumptionRecords");
        migrationBuilder.DropColumn(name: "ReimbursementStatus", table: "ConsumptionRecords");
    }
}
