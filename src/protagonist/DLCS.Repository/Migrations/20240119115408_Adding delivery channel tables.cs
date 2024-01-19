using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DLCS.Repository.Migrations
{
    public partial class Addingdeliverychanneltables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryChannelPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    Customer = table.Column<int>(type: "integer", nullable: false),
                    Space = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PolicyCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PolicyModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PolicyData = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryChannelPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImageDeliveryChannels",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImageId = table.Column<string>(type: "character varying(500)", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    DeliveryChannelPolicyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageDeliveryChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageDeliveryChannels_DeliveryChannelPolicies_DeliveryChann~",
                        column: x => x.DeliveryChannelPolicyId,
                        principalTable: "DeliveryChannelPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImageDeliveryChannels_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChannelPolicies_Name_Customer_Space",
                table: "DeliveryChannelPolicies",
                columns: new[] { "Name", "Customer", "Space" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageDeliveryChannels_DeliveryChannelPolicyId",
                table: "ImageDeliveryChannels",
                column: "DeliveryChannelPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageDeliveryChannels_ImageId",
                table: "ImageDeliveryChannels",
                column: "ImageId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageDeliveryChannels");

            migrationBuilder.DropTable(
                name: "DeliveryChannelPolicies");
        }
    }
}
