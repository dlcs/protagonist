using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    public partial class Createnotranscodepolicy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ImageOptimisationPolicies",
                columns: new[] { "Customer", "Id", "Global", "Name", "TechnicalDetails" },
                values: new object[,]
                {
                    { 1, "none", true, "No optimisation/transcoding", "no-op" },
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ImageOptimisationPolicies",
                keyColumns: new[] { "Customer", "Id" },
                keyValues: new object[] { 1, "none" });
        }
    }
}
