using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    /// <inheritdoc />
    public partial class Addadjunctstable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Adjuncts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AssetId = table.Column<string>(type: "character varying(500)", nullable: false),
                    MediaType = table.Column<string>(type: "text", nullable: false),
                    IIIFLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Profile = table.Column<string>(type: "text", nullable: true),
                    Label = table.Column<string>(type: "jsonb", nullable: true),
                    Language = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Finished = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adjuncts", x => new { x.Id, x.AssetId });
                    table.ForeignKey(
                        name: "FK_Adjuncts_Images_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Adjuncts_AssetId",
                table: "Adjuncts",
                column: "AssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Adjuncts");
        }
    }
}
