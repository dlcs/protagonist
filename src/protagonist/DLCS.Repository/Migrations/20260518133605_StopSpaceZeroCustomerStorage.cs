using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DLCS.Repository.Migrations
{
    /// <inheritdoc />
    public partial class StopSpaceZeroCustomerStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CustomerStorage",
                table: "CustomerStorage");

            migrationBuilder.AlterColumn<int>(
                name: "Space",
                table: "CustomerStorage",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "CustomerStorage",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CustomerStorage",
                table: "CustomerStorage",
                column: "Id");

            // All existing Space=0 rows were aggregate rows written by the recalculators.
            // Real stub-asset Space=0 tracking has not started yet, so this is safe to do wholesale.
            migrationBuilder.Sql(@"UPDATE ""CustomerStorage"" SET ""Space"" = NULL WHERE ""Space"" = 0;");

            // Ensure every customer has an aggregate (NULL-space) row; some may never have had a Space=0 row.
            migrationBuilder.Sql(@"
INSERT INTO ""CustomerStorage"" (""Customer"", ""Space"", ""StoragePolicy"", ""NumberOfStoredImages"", ""TotalSizeOfStoredImages"", ""TotalSizeOfThumbnails"")
SELECT c.""Id"", NULL, 'default', 0, 0, 0
FROM ""Customers"" c
WHERE NOT EXISTS (
    SELECT 1 FROM ""CustomerStorage"" cs WHERE cs.""Customer"" = c.""Id"" AND cs.""Space"" IS NULL
);");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerStorage_Customer_Aggregate",
                table: "CustomerStorage",
                column: "Customer",
                unique: true,
                filter: "\"Space\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerStorage_Customer_Space",
                table: "CustomerStorage",
                columns: new[] { "Customer", "Space" },
                unique: true,
                filter: "\"Space\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CustomerStorage",
                table: "CustomerStorage");

            // NOTE: Down is only safe before any real Space=0 stub-asset storage rows have been written.
            // If both a NULL aggregate row and a real Space=0 row exist for the same customer,
            // restoring the {Customer, Space} composite PK will conflict. Resolve manually before running Down.
            migrationBuilder.Sql(@"UPDATE ""CustomerStorage"" SET ""Space"" = 0 WHERE ""Space"" IS NULL;");

            migrationBuilder.DropIndex(
                name: "IX_CustomerStorage_Customer_Aggregate",
                table: "CustomerStorage");

            migrationBuilder.DropIndex(
                name: "IX_CustomerStorage_Customer_Space",
                table: "CustomerStorage");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "CustomerStorage");

            migrationBuilder.AlterColumn<int>(
                name: "Space",
                table: "CustomerStorage",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CustomerStorage",
                table: "CustomerStorage",
                columns: new[] { "Customer", "Space" });
        }
    }
}
