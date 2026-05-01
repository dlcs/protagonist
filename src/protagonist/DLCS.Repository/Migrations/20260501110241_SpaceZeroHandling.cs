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

            // Add space 0 (stub-space) for all existing customers that don't already have one
            migrationBuilder.Sql(@"
INSERT INTO ""Spaces"" (""Id"", ""Customer"", ""Name"", ""Created"", ""ImageBucket"", ""Tags"", ""Roles"", ""MaxUnauthorised"")
SELECT 0, c.""Id"", 'stub-space', NOW(), '', ARRAY[]::text[], ARRAY[]::text[], -1
FROM ""Customers"" c
WHERE NOT EXISTS (
    SELECT 1 FROM ""Spaces"" s WHERE s.""Customer"" = c.""Id"" AND s.""Id"" = 0
);
");

            // Add 'none' default delivery channel for customer 1 space 0 (the template used for new customers)
            migrationBuilder.InsertData(
                table: "DefaultDeliveryChannels",
                columns: new[] { "Id", "Customer", "DeliveryChannelPolicyId", "MediaType", "Space" },
                values: new object[] { Guid.NewGuid(), 1, Ids.None, "*/*", 0 });

            // Add 'none' default delivery channel for all existing non-customer-1 customers
            migrationBuilder.Sql(@"
INSERT INTO ""DefaultDeliveryChannels"" (""Id"", ""Customer"", ""DeliveryChannelPolicyId"", ""MediaType"", ""Space"")
SELECT gen_random_uuid(), c.""Id"", " + Ids.None + @", '*/*', 0
FROM ""Customers"" c
WHERE c.""Id"" != 1;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove 'none' default delivery channel entries for space 0
            migrationBuilder.Sql(@"
DELETE FROM ""DefaultDeliveryChannels""
WHERE ""DeliveryChannelPolicyId"" = " + Ids.None + @" AND ""Space"" = 0 AND ""MediaType"" = '*/*';
");

            // Remove space 0 for all customers (stub-space entries added by this migration)
            migrationBuilder.Sql(@"
DELETE FROM ""Spaces"" WHERE ""Id"" = 0 AND ""Name"" = 'stub-space';
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