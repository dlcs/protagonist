using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    /// <inheritdoc />
    public partial class BackfillAdjunctQueueSize : Migration
    {
        /// <summary>
        /// Resets every "adjunct" Queues row to 0. Engine was decrementing the wrong Queues row for completed
        /// adjuncts (see the IngestHandler fix in the same PR), so any "adjunct" row created before that fix may
        /// have drifted upwards and never come back down.  Exposed as a constant so it can be exercised directly by
        /// <c>BackfillAdjunctQueueSizeMigrationTests</c> without duplicating the SQL.
        /// </summary>
        public const string CorrectionSql = @"UPDATE ""Queues"" SET ""Size"" = 0 WHERE ""Name"" = 'adjunct';";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CorrectionSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op - this is a one-off data correction, there is no prior value to restore to.
        }
    }
}
