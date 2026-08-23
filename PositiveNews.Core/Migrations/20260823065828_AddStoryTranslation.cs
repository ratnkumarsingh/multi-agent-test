using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PositiveNews.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryTranslation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoryTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NewsStoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Locale = table.Column<string>(type: "TEXT", nullable: false),
                    Headline = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryTranslations_NewsStories_NewsStoryId",
                        column: x => x.NewsStoryId,
                        principalTable: "NewsStories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoryTranslations_NewsStoryId_Locale",
                table: "StoryTranslations",
                columns: new[] { "NewsStoryId", "Locale" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoryTranslations");
        }
    }
}
