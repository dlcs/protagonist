using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    public partial class addingAssetApplicationMetadatatable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetApplicationMetadata",
                columns: table => new
                {
                    AssetId = table.Column<string>(type: "character varying(500)", nullable: false),
                    MetadataType = table.Column<string>(type: "text", nullable: false),
                    MetadataValue = table.Column<string>(type: "jsonb", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetApplicationMetadata", x => new { x.AssetId, x.MetadataType });
                    table.ForeignKey(
                        name: "FK_AssetApplicationMetadata_Images_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetApplicationMetadata");
        }
    }
}
