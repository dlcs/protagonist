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
                    Channel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    System = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PolicyData = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryChannelPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DefaultDeliveryChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Customer = table.Column<int>(type: "integer", nullable: false),
                    Space = table.Column<int>(type: "integer", nullable: false),
                    MediaType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DeliveryChannelPolicyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefaultDeliveryChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DefaultDeliveryChannels_DeliveryChannelPolicies_DeliveryCha~",
                        column: x => x.DeliveryChannelPolicyId,
                        principalTable: "DeliveryChannelPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImageDeliveryChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", maxLength: 100, nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
                name: "IX_DefaultDeliveryChannels_Customer_Space_MediaType_DeliveryCh~",
                table: "DefaultDeliveryChannels",
                columns: new[] { "Customer", "Space", "MediaType", "DeliveryChannelPolicyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DefaultDeliveryChannels_DeliveryChannelPolicyId",
                table: "DefaultDeliveryChannels",
                column: "DeliveryChannelPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChannelPolicies_Customer_Name",
                table: "DeliveryChannelPolicies",
                columns: new[] { "Customer", "Name" },
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
                name: "DefaultDeliveryChannels");

            migrationBuilder.DropTable(
                name: "ImageDeliveryChannels");

            migrationBuilder.DropTable(
                name: "DeliveryChannelPolicies");
        }
    }
}
