using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class UseDomainKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PageScores_Url",
                table: "PageScores");

            // Merge duplicate domain rows: keep the one with the highest CheckCount,
            // aggregate counts and scores, then delete the rest.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", "Domain",
                           ROW_NUMBER() OVER (PARTITION BY "Domain" ORDER BY "CheckCount" DESC, "Id") AS rn
                    FROM "PageScores"
                ),
                agg AS (
                    SELECT "Domain",
                           SUM("SecurityCheckCount") AS total_sec,
                           SUM("AiCheckCount") AS total_ai,
                           SUM("CheckCount") AS total_checks,
                           MAX("LastChecked") AS last_checked,
                           CASE WHEN SUM("SecurityCheckCount") > 0
                                THEN ROUND(SUM("SecurityScore"::numeric * "SecurityCheckCount") / SUM("SecurityCheckCount"))
                                ELSE 0 END AS avg_sec,
                           CASE WHEN SUM("AiCheckCount") > 0
                                THEN ROUND(SUM("AiScore"::numeric * "AiCheckCount") / SUM("AiCheckCount"))
                                ELSE 0 END AS avg_ai
                    FROM "PageScores"
                    GROUP BY "Domain"
                    HAVING COUNT(*) > 1
                )
                UPDATE "PageScores" p
                SET "SecurityScore" = a.avg_sec::int,
                    "AiScore" = a.avg_ai::int,
                    "SecurityCheckCount" = a.total_sec,
                    "AiCheckCount" = a.total_ai,
                    "CheckCount" = a.total_checks,
                    "LastChecked" = a.last_checked,
                    "Url" = p."Domain"
                FROM agg a
                JOIN ranked r ON r."Domain" = a."Domain" AND r.rn = 1
                WHERE p."Id" = r."Id";
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", "Domain",
                           ROW_NUMBER() OVER (PARTITION BY "Domain" ORDER BY "CheckCount" DESC, "Id") AS rn
                    FROM "PageScores"
                )
                DELETE FROM "PageScores"
                WHERE "Id" IN (SELECT "Id" FROM ranked WHERE rn > 1);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PageScores_Domain",
                table: "PageScores",
                column: "Domain",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PageScores_Domain",
                table: "PageScores");

            migrationBuilder.CreateIndex(
                name: "IX_PageScores_Url",
                table: "PageScores",
                column: "Url",
                unique: true);
        }
    }
}
