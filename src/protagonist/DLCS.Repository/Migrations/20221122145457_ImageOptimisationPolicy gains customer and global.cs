using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    public partial class ImageOptimisationPolicygainscustomerandglobal : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Customer",
                table: "ImageOptimisationPolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Global",
                table: "ImageOptimisationPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Prior to introducing 'Global' column all policies were global so update to reflect this
            migrationBuilder.Sql("UPDATE \"ImageOptimisationPolicies\" SET \"Global\" = true;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Customer",
                table: "ImageOptimisationPolicies");

            migrationBuilder.DropColumn(
                name: "Global",
                table: "ImageOptimisationPolicies");
        }
    }
}
