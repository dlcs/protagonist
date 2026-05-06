using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Ids = DLCS.Model.Policies.KnownDeliveryChannelPolicies;

#nullable disable

namespace DLCS.Repository.Migrations
{
    public partial class SpaceZeroHandling : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make Space nullable so NULL can represent customer-wide defaults
            migrationBuilder.AlterColumn<int>(
                name: "Space",
                table: "DefaultDeliveryChannels",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            // Existing Space=0 rows were the customer-wide defaults; move them to NULL
            migrationBuilder.Sql(@"UPDATE ""DefaultDeliveryChannels"" SET ""Space"" = NULL WHERE ""Space"" = 0;");

            // Add space 0 (stub-assets) for all existing customers that don't already have one
            migrationBuilder.Sql(@"
INSERT INTO ""Spaces"" (""Id"", ""Customer"", ""Name"", ""Created"", ""ImageBucket"", ""Tags"", ""Roles"", ""MaxUnauthorised"", ""Keep"", ""Transform"")
SELECT 0, c.""Id"", 'stub-assets', NOW(), '', ARRAY[]::text[], ARRAY[]::text[], -1, false, false
FROM ""Customers"" c
WHERE NOT EXISTS (
    SELECT 1 FROM ""Spaces"" s WHERE s.""Customer"" = c.""Id"" AND s.""Id"" = 0
);
");

            // Add 'none' default delivery channel at space 0 for customer 1 (template for new customers).
            migrationBuilder.Sql(@"
INSERT INTO ""DefaultDeliveryChannels"" (""Id"", ""Customer"", ""DeliveryChannelPolicyId"", ""MediaType"", ""Space"")
SELECT gen_random_uuid(), 1, " + Ids.None + @", '*/*', 0
WHERE NOT EXISTS (
    SELECT 1 FROM ""DefaultDeliveryChannels"" ddc
    WHERE ddc.""Customer"" = 1
      AND ddc.""Space"" = 0
      AND ddc.""MediaType"" = '*/*'
      AND ddc.""DeliveryChannelPolicyId"" = " + Ids.None + @"
);
");

            // Add 'none' default delivery channel at space 0 for all other existing customers
            migrationBuilder.Sql(@"
INSERT INTO ""DefaultDeliveryChannels"" (""Id"", ""Customer"", ""DeliveryChannelPolicyId"", ""MediaType"", ""Space"")
SELECT gen_random_uuid(), c.""Id"", " + Ids.None + @", '*/*', 0
FROM ""Customers"" c
WHERE c.""Id"" != 1
  AND NOT EXISTS (
    SELECT 1 FROM ""DefaultDeliveryChannels"" ddc
    WHERE ddc.""Customer"" = c.""Id""
      AND ddc.""Space"" = 0
      AND ddc.""MediaType"" = '*/*'
      AND ddc.""DeliveryChannelPolicyId"" = " + Ids.None + @"
);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove 'none' default delivery channel entries for space 0
            migrationBuilder.Sql(@"
DELETE FROM ""DefaultDeliveryChannels""
WHERE ""DeliveryChannelPolicyId"" = " + Ids.None + @" AND ""Space"" = 0 AND ""MediaType"" = '*/*';
");

            // Remove space 0 for all customers (stub-assets entries added by this migration)
            migrationBuilder.Sql(@"
DELETE FROM ""Spaces"" WHERE ""Id"" = 0 AND ""Name"" = 'stub-assets';
");

            // Restore NULL back to 0 for the customer-wide defaults
            migrationBuilder.Sql(@"UPDATE ""DefaultDeliveryChannels"" SET ""Space"" = 0 WHERE ""Space"" IS NULL;");

            // Restore column to NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "Space",
                table: "DefaultDeliveryChannels",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldNullable: true,
                oldType: "integer");
        }
    }
}
