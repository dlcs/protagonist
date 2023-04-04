using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    public partial class PluraliseImagesDeliveryChannel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DeliveryChannel",
                table: "Images",
                newName: "DeliveryChannels");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DeliveryChannels",
                table: "Images",
                newName: "DeliveryChannel");
        }
    }
}
