using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    /// <inheritdoc />
    public partial class HostedAdjunctProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ExternalId",
                table: "Adjuncts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Error",
                table: "Adjuncts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Ingesting",
                table: "Adjuncts",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Adjuncts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Error",
                table: "Adjuncts");

            migrationBuilder.DropColumn(
                name: "Ingesting",
                table: "Adjuncts");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Adjuncts");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalId",
                table: "Adjuncts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
