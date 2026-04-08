using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PowerTech.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryFailCountToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeliveryFailCount",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryFailCount",
                table: "Orders");
        }
    }
}
