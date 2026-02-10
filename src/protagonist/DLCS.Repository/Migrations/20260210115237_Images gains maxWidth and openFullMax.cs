using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ImagesgainsmaxWidthandopenFullMax : Migration
    {
        private const string UnobtainableRole = "https://dlcs.io/roles/unobtainable";
        
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxWidth",
                table: "Images",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OpenFullMax",
                table: "Images",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            
            /*
             With introduction of above fields "MaxUnauthorised" is deprecated. The below script will update Images
             table to maintain same behaviour as seen previously. The below rules are based on those from RFC
             https://github.com/dlcs/protagonist/blob/develop/docs/adr/0010-replace-maxunauthorised.md#use-case--behaviours
             */
            migrationBuilder.Sql($@"
WITH calculated_values AS (
    SELECT
        ""Id"",
        CASE
            WHEN ""MaxUnauthorised"" = -1 and ""Roles"" = '' THEN 'all' -- all sizes/regions to all
            WHEN ""MaxUnauthorised"" = 0 and ""Roles"" = '' THEN 'none' -- no sizes/regions to anyone
            WHEN ""MaxUnauthorised"" <= 0 and ""Roles"" != '' THEN 'need-role' -- only users with role can see anything
            WHEN ""MaxUnauthorised"" > 0 and ""Roles"" = '' THEN 'full-only' -- full requests available to size, tiles unavailable
            WHEN ""MaxUnauthorised"" > 0 and ""Roles"" != '' THEN 'zoom-with-role' -- as above but users with role can see larger/alt regions
            END as classification
    FROM ""Images""
)
UPDATE ""Images"" i
SET 
    ""OpenFullMax"" = CASE classification
        WHEN 'full-only' THEN ""MaxUnauthorised""
        WHEN 'zoom-with-role' THEN ""MaxUnauthorised"" 
        ELSE 0
        END,
    ""Roles"" = CASE classification
        WHEN 'none' THEN '{UnobtainableRole}'
        WHEN 'full-only' THEN '{UnobtainableRole}'
        ELSE ""Roles""
        END
from
    calculated_values cv
WHERE i.""Id"" = cv.""Id"";
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The above uses a hardcoded role in some scenarios, if we need to revert remove this
            migrationBuilder.Sql(
                $"UPDATE \"Images\" SET \"Roles\" = '' WHERE \"Roles\" = '{UnobtainableRole}';");
            
            migrationBuilder.DropColumn(
                name: "MaxWidth",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "OpenFullMax",
                table: "Images");
        }
    }
}
