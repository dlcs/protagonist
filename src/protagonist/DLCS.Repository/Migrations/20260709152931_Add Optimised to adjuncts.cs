using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimisedtoadjuncts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Optimised",
                table: "Adjuncts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Optimised",
                table: "Adjuncts");
        }
    }
}
