using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    public partial class AddCredibilityScoreToPageScore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CredibilityCheckCount",
                table: "PageScores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CredibilityScore",
                table: "PageScores",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CredibilityCheckCount",
                table: "PageScores");

            migrationBuilder.DropColumn(
                name: "CredibilityScore",
                table: "PageScores");
        }
    }
}
