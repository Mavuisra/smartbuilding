using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartBuilding.Infrastructure.Migrations.MySql
{
    /// <inheritdoc />
    public partial class AddConsumptionCustomTypeLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomTypeLabel",
                table: "ConsumptionRecords",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomTypeLabel",
                table: "ConsumptionRecords");
        }
    }
}
