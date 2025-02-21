using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DLCS.Repository.Migrations;

public partial class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:tablefunc", ",,");

        migrationBuilder.CreateSequence(
            name: "batch_id_sequence",
            startValue: 570185L,
            minValue: 1L,
            maxValue: 9223372036854775807L);

        migrationBuilder.CreateTable(
            name: "ActivityGroups",
            columns: table => new
            {
                Group = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Since = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Inhabitant = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActivityGroups", x => x.Group);
            });

        migrationBuilder.CreateTable(
            name: "AuthServices",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Customer = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                Profile = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Label = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                PageLabel = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                PageDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CallToAction = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                TTL = table.Column<int>(type: "integer", nullable: false),
                RoleProvider = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ChildAuthService = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthServices", x => new { x.Id, x.Customer });
            });

        migrationBuilder.CreateTable(
            name: "AuthTokens",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Customer = table.Column<int>(type: "integer", nullable: false),
                Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Expires = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastChecked = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CookieId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                SessionUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                BearerToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                TTL = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthTokens", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Batches",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('batch_id_sequence'::regclass)"),
                Customer = table.Column<int>(type: "integer", nullable: false),
                Submitted = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Count = table.Column<int>(type: "integer", nullable: false),
                Completed = table.Column<int>(type: "integer", nullable: false),
                Errors = table.Column<int>(type: "integer", nullable: false),
                Finished = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Superseded = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Batches", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CustomerImageServers",
            columns: table => new
            {
                Customer = table.Column<int>(type: "integer", nullable: false),
                ImageServer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomerImageServers", x => x.Customer);
            });

        migrationBuilder.CreateTable(
            name: "CustomerOriginStrategies",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Customer = table.Column<int>(type: "integer", nullable: false),
                Regex = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Strategy = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Credentials = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Optimised = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomerOriginStrategies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Keys = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Administrator = table.Column<bool>(type: "boolean", nullable: false),
                Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                AcceptedAgreement = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CustomerStorage",
            columns: table => new
            {
                Customer = table.Column<int>(type: "integer", nullable: false),
                Space = table.Column<int>(type: "integer", nullable: false),
                StoragePolicy = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                NumberOfStoredImages = table.Column<long>(type: "bigint", nullable: false),
                TotalSizeOfStoredImages = table.Column<long>(type: "bigint", nullable: false),
                TotalSizeOfThumbnails = table.Column<long>(type: "bigint", nullable: false),
                LastCalculated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomerStorage", x => new { x.Customer, x.Space });
            });

        migrationBuilder.CreateTable(
            name: "CustomHeaders",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Customer = table.Column<int>(type: "integer", nullable: false),
                Space = table.Column<int>(type: "integer", nullable: true),
                Role = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, defaultValueSql: "NULL::character varying"),
                Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomHeaders", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "EntityCounters",
            columns: table => new
            {
                Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Scope = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Customer = table.Column<int>(type: "integer", nullable: false),
                Next = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EntityCounters", x => new { x.Type, x.Scope, x.Customer });
            });

        migrationBuilder.CreateTable(
            name: "ImageLocation",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                S3 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Nas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImageLocation", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ImageOptimisationPolicies",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                TechnicalDetails = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImageOptimisationPolicies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Images",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Customer = table.Column<int>(type: "integer", nullable: false),
                Space = table.Column<int>(type: "integer", nullable: false),
                Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Origin = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Tags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Roles = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                PreservedUri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Reference1 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Reference2 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Reference3 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                NumberReference1 = table.Column<int>(type: "integer", nullable: false),
                NumberReference2 = table.Column<int>(type: "integer", nullable: false),
                NumberReference3 = table.Column<int>(type: "integer", nullable: false),
                MaxUnauthorised = table.Column<int>(type: "integer", nullable: false),
                Width = table.Column<int>(type: "integer", nullable: false),
                Height = table.Column<int>(type: "integer", nullable: false),
                Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValueSql: "NULL::character varying"),
                Batch = table.Column<int>(type: "integer", nullable: false),
                Finished = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Ingesting = table.Column<bool>(type: "boolean", nullable: false),
                ImageOptimisationPolicy = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValueSql: "'fast-lossy'::character varying"),
                ThumbnailPolicy = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValueSql: "'original'::character varying"),
                Family = table.Column<string>(type: "char(1)", nullable: false, defaultValueSql: "'I'::\"char\""),
                MediaType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "'image/jp2'::character varying"),
                Duration = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "0")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Images", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ImageServers",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                InfoJsonTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImageServers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ImageStorage",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Customer = table.Column<int>(type: "integer", nullable: false),
                Space = table.Column<int>(type: "integer", nullable: false),
                ThumbnailSize = table.Column<long>(type: "bigint", nullable: false),
                Size = table.Column<long>(type: "bigint", nullable: false),
                LastChecked = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CheckingInProgress = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImageStorage", x => new { x.Id, x.Customer, x.Space });
            });

        migrationBuilder.CreateTable(
            name: "InfoJsonTemplates",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Template = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InfoJsonTemplates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "MetricThresholds",
            columns: table => new
            {
                Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Metric = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Lower = table.Column<long>(type: "bigint", nullable: true),
                Upper = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MetricThresholds", x => new { x.Name, x.Metric });
            });

        migrationBuilder.CreateTable(
            name: "NamedQueries",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Customer = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Global = table.Column<bool>(type: "boolean", nullable: false),
                Template = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NamedQueries", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OriginStrategies",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                RequiresCredentials = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OriginStrategies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Queues",
            columns: table => new
            {
                Customer = table.Column<int>(type: "integer", nullable: false),
                Size = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValueSql: "'default'::character varying")
            },
            constraints: table =>
            {
                table.PrimaryKey("Queues_pkey", x => x.Customer);
            });

        migrationBuilder.CreateTable(
            name: "RoleProviders",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Customer = table.Column<int>(type: "integer", nullable: false),
                AuthService = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Configuration = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                Credentials = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RoleProviders", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Customer = table.Column<int>(type: "integer", nullable: false),
                AuthService = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Aliases = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => new { x.Id, x.Customer });
            });

        migrationBuilder.CreateTable(
            name: "SessionUsers",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Roles = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SessionUsers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Spaces",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                Customer = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ImageBucket = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Tags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Roles = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Keep = table.Column<bool>(type: "boolean", nullable: false),
                Transform = table.Column<bool>(type: "boolean", nullable: false),
                MaxUnauthorised = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("Spaces_pkey", x => new { x.Id, x.Customer });
            });

        migrationBuilder.CreateTable(
            name: "StoragePolicies",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                MaximumNumberOfStoredImages = table.Column<long>(type: "bigint", nullable: false),
                MaximumTotalSizeOfStoredImages = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoragePolicies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ThumbnailPolicies",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Sizes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ThumbnailPolicies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Customer = table.Column<int>(type: "integer", nullable: false),
                Email = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                EncryptedPassword = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                Roles = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuthTokens_BearerToken",
            table: "AuthTokens",
            column: "BearerToken");

        migrationBuilder.CreateIndex(
            name: "IX_AuthTokens_CookieId",
            table: "AuthTokens",
            column: "CookieId");

        migrationBuilder.CreateIndex(
            name: "IX_BatchTest",
            table: "Batches",
            columns: new[] { "Customer", "Superseded", "Submitted" });

        migrationBuilder.CreateIndex(
            name: "IX_CustomHeaders_ByCustomerSpace",
            table: "CustomHeaders",
            columns: new[] { "Customer", "Space" });

        migrationBuilder.CreateIndex(
            name: "IX_ImagesByBatch",
            table: "Images",
            column: "Batch");

        migrationBuilder.CreateIndex(
            name: "IX_ImagesByCustomerSpace",
            table: "Images",
            columns: new[] { "Id", "Customer", "Space" });

        migrationBuilder.CreateIndex(
            name: "IX_ImagesByErrors",
            table: "Images",
            columns: new[] { "Id", "Customer", "Error", "Batch" },
            filter: "((\"Error\" IS NOT NULL) AND ((\"Error\")::text <> ''::text))");

        migrationBuilder.CreateIndex(
            name: "IX_ImagesByReference1",
            table: "Images",
            column: "Reference1");

        migrationBuilder.CreateIndex(
            name: "IX_ImagesByReference2",
            table: "Images",
            column: "Reference2");

        migrationBuilder.CreateIndex(
            name: "IX_ImagesByReference3",
            table: "Images",
            column: "Reference3");

        migrationBuilder.CreateIndex(
            name: "IX_ImagesBySpace",
            table: "Images",
            columns: new[] { "Customer", "Space" });

        migrationBuilder.CreateIndex(
            name: "IX_ImageStorageByCustomerSpace",
            table: "ImageStorage",
            columns: new[] { "Customer", "Space", "Id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ActivityGroups");

        migrationBuilder.DropTable(
            name: "AuthServices");

        migrationBuilder.DropTable(
            name: "AuthTokens");

        migrationBuilder.DropTable(
            name: "Batches");

        migrationBuilder.DropTable(
            name: "CustomerImageServers");

        migrationBuilder.DropTable(
            name: "CustomerOriginStrategies");

        migrationBuilder.DropTable(
            name: "Customers");

        migrationBuilder.DropTable(
            name: "CustomerStorage");

        migrationBuilder.DropTable(
            name: "CustomHeaders");

        migrationBuilder.DropTable(
            name: "EntityCounters");

        migrationBuilder.DropTable(
            name: "ImageLocation");

        migrationBuilder.DropTable(
            name: "ImageOptimisationPolicies");

        migrationBuilder.DropTable(
            name: "Images");

        migrationBuilder.DropTable(
            name: "ImageServers");

        migrationBuilder.DropTable(
            name: "ImageStorage");

        migrationBuilder.DropTable(
            name: "InfoJsonTemplates");

        migrationBuilder.DropTable(
            name: "MetricThresholds");

        migrationBuilder.DropTable(
            name: "NamedQueries");

        migrationBuilder.DropTable(
            name: "OriginStrategies");

        migrationBuilder.DropTable(
            name: "Queues");

        migrationBuilder.DropTable(
            name: "RoleProviders");

        migrationBuilder.DropTable(
            name: "Roles");

        migrationBuilder.DropTable(
            name: "SessionUsers");

        migrationBuilder.DropTable(
            name: "Spaces");

        migrationBuilder.DropTable(
            name: "StoragePolicies");

        migrationBuilder.DropTable(
            name: "ThumbnailPolicies");

        migrationBuilder.DropTable(
            name: "Users");

        migrationBuilder.DropSequence(
            name: "batch_id_sequence");
    }
}
