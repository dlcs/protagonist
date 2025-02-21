using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    public partial class ImageOptimisationPolicygainscompositekey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"ImageOptimisationPolicies\" SET \"Customer\" = 1 WHERE \"Customer\" IS NULL;");
            
            migrationBuilder.DropPrimaryKey(
                name: "PK_ImageOptimisationPolicies",
                table: "ImageOptimisationPolicies");

            migrationBuilder.AlterColumn<int>(
                name: "Customer",
                table: "ImageOptimisationPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ImageOptimisationPolicies",
                table: "ImageOptimisationPolicies",
                columns: new[] { "Id", "Customer" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ImageOptimisationPolicies",
                table: "ImageOptimisationPolicies");

            migrationBuilder.AlterColumn<int>(
                name: "Customer",
                table: "ImageOptimisationPolicies",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ImageOptimisationPolicies",
                table: "ImageOptimisationPolicies",
                column: "Id");
        }
    }
}
