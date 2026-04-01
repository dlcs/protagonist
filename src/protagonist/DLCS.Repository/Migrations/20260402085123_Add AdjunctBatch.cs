using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddAdjunctBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "adjunct_batch_id_sequence",
                minValue: 1L);

            migrationBuilder.AddColumn<int>(
                name: "Batch",
                table: "Adjuncts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdjunctBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('adjunct_batch_id_sequence'::regclass)"),
                    Customer = table.Column<int>(type: "integer", nullable: false),
                    Submitted = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    Completed = table.Column<int>(type: "integer", nullable: false),
                    Errors = table.Column<int>(type: "integer", nullable: false),
                    Finished = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjunctBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdjunctBatchAdjuncts",
                columns: table => new
                {
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    AdjunctId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssetId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    Finished = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjunctBatchAdjuncts", x => new { x.BatchId, x.AdjunctId, x.AssetId });
                    table.ForeignKey(
                        name: "FK_AdjunctBatchAdjuncts_AdjunctBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "AdjunctBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Adjuncts_Batch",
                table: "Adjuncts",
                column: "Batch");

            migrationBuilder.CreateIndex(
                name: "IX_AdjunctBatchesByCustomerSubmitted",
                table: "AdjunctBatches",
                columns: new[] { "Customer", "Submitted" });

            migrationBuilder.AddForeignKey(
                name: "FK_Adjuncts_AdjunctBatches_Batch",
                table: "Adjuncts",
                column: "Batch",
                principalTable: "AdjunctBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Adjuncts_AdjunctBatches_Batch",
                table: "Adjuncts");

            migrationBuilder.DropTable(
                name: "AdjunctBatchAdjuncts");

            migrationBuilder.DropTable(
                name: "AdjunctBatches");

            migrationBuilder.DropIndex(
                name: "IX_Adjuncts_Batch",
                table: "Adjuncts");

            migrationBuilder.DropColumn(
                name: "Batch",
                table: "Adjuncts");

            migrationBuilder.DropSequence(
                name: "adjunct_batch_id_sequence");
        }
    }
}
