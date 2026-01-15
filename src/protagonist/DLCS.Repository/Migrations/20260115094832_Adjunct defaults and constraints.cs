using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    /// <inheritdoc />
    public partial class Adjunctdefaultsandconstraints : Migration
    {
        private const string IndexName = "IX_Adjuncts_Id";
        
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "Created",
                table: "Adjuncts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Adjuncts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);
            
            // This can't be modelled by EFCore, so instead we create manually
            // see https://github.com/npgsql/efcore.pg/issues/119
            migrationBuilder.Sql($"CREATE UNIQUE INDEX \"{IndexName}\" ON \"Adjuncts\" (LOWER(\"Id\"), \"AssetId\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "Created",
                table: "Adjuncts",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Adjuncts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
            
            migrationBuilder.Sql($"DROP INDEX \"{IndexName}\";");
        }
    }
}
