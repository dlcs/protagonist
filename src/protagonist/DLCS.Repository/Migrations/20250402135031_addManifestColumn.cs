using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    /// <inheritdoc />
    public partial class addManifestColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Manifests",
                table: "Images",
                type: "text[]",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_Manifests",
                table: "Images",
                column: "Manifests")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Images_Manifests",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "Manifests",
                table: "Images");
        }
    }
}
