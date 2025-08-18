using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    /// <inheritdoc />
    public partial class RemovemanifestNQprop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove 'manifest' NQ parameter as it is being repurposed but is present in a lot of historical templates 
            migrationBuilder.Sql(@"
UPDATE ""NamedQueries""
SET ""Template"" = regexp_replace(""Template"", '^manifest=[\w\d]+&', '')
WHERE ""Template"" like '%manifest%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
