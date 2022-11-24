using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    public partial class Imagegainsdeliverychannel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryChannel",
                table: "Images",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryChannel",
                table: "Images");
        }
    }
}
