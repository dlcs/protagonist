using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    public partial class Queuegainscompositekey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "Queues_pkey",
                table: "Queues");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Queues",
                table: "Queues",
                columns: new[] { "Customer", "Name" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Queues",
                table: "Queues");

            migrationBuilder.AddPrimaryKey(
                name: "Queues_pkey",
                table: "Queues",
                column: "Customer");
        }
    }
}
