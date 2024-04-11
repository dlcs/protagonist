using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                    Id = table.Column<int>(type: "integer", maxLength: 100, nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImageId = table.Column<string>(type: "character varying(500)", nullable: false),
                    MetadataType = table.Column<string>(type: "text", nullable: false),
                    MetadataValue = table.Column<string>(type: "jsonb", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetApplicationMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetApplicationMetadata_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetApplicationMetadata_ImageId",
                table: "AssetApplicationMetadata",
                column: "ImageId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetApplicationMetadata");
        }
    }
}
