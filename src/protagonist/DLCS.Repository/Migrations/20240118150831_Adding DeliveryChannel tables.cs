using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    public partial class AddingDeliveryChanneltables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryChannelPolicies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Customer = table.Column<int>(type: "integer", nullable: false),
                    Space = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    Channel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PolicyCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PolicyModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PolicyData = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("DeliveryChannelPolicy_pkey", x => new { x.Id, x.Customer, x.Space });
                });

            migrationBuilder.CreateTable(
                name: "ImageDeliveryChannels",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImageId = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Policy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageDeliveryChannels", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryChannelPolicies");

            migrationBuilder.DropTable(
                name: "ImageDeliveryChannels");
        }
    }
}
