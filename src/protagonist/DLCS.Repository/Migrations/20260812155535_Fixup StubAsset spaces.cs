using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    /// <inheritdoc />
    public partial class FixupStubAssetspaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SpaceZeroHandling set "Tags" and "Roles" to ARRAY[]::text[], which results in '{}'. This resets
            // those to and empty string.
            migrationBuilder.Sql("UPDATE \"Spaces\" SET \"Tags\" = '' WHERE \"Tags\" = '{}' and \"Id\" = 0;");
            migrationBuilder.Sql("UPDATE \"Spaces\" SET \"Roles\" = '' WHERE \"Roles\" = '{}' and \"Id\" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No down script, this was in error so no need to set it back.
        }
    }
}
