using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PositiveNews.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddLocaleToCandidateAndStory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Locale",
                table: "PipelineCandidates",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Locale",
                table: "NewsStories",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Locale",
                table: "PipelineCandidates");

            migrationBuilder.DropColumn(
                name: "Locale",
                table: "NewsStories");
        }
    }
}
