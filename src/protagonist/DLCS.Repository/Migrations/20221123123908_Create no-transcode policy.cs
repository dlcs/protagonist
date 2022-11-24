using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    public partial class Createnotranscodepolicy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO ""ImageOptimisationPolicies"" (""Id"", ""Name"", ""TechnicalDetails"", ""Customer"", ""Global"")
VALUES ('none', 'No optimisation/transcoding', 'no-op', 1, true) ON CONFLICT DO NOTHING;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"ImageOptimisationPolicies\" WHERE \"Id\" = 'none';");
        }
    }
}
