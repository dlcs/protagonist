using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DLCS.Repository.Migrations
{
    public partial class AddSignupLinks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<char>(
                name: "Family",
                table: "Images",
                type: "character(1)",
                nullable: false,
                defaultValueSql: "'I'::\"char\"",
                oldClrType: typeof(string),
                oldType: "char(1)",
                oldDefaultValueSql: "'I'::\"char\"");

            migrationBuilder.CreateTable(
                name: "SignupLinks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Expires = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignupLinks", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignupLinks");

            migrationBuilder.AlterColumn<string>(
                name: "Family",
                table: "Images",
                type: "char(1)",
                nullable: false,
                defaultValueSql: "'I'::\"char\"",
                oldClrType: typeof(char),
                oldType: "character(1)",
                oldDefaultValueSql: "'I'::\"char\"");
        }
    }
}
