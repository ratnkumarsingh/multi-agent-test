using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PositiveNews.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PipelineRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TopicHint = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsStories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PipelineRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    Headline = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsStories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsStories_PipelineRuns_PipelineRunId",
                        column: x => x.PipelineRunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipelineCandidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PipelineRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Snippet = table.Column<string>(type: "TEXT", nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    ScoreReasoning = table.Column<string>(type: "TEXT", nullable: true),
                    IsPositive = table.Column<bool>(type: "INTEGER", nullable: true),
                    Summarized = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineCandidates_PipelineRuns_PipelineRunId",
                        column: x => x.PipelineRunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipelineSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PipelineRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", nullable: false),
                    ItemLabel = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineSteps_PipelineRuns_PipelineRunId",
                        column: x => x.PipelineRunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsStories_PipelineRunId",
                table: "NewsStories",
                column: "PipelineRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineCandidates_PipelineRunId_Url",
                table: "PipelineCandidates",
                columns: new[] { "PipelineRunId", "Url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_RunDate",
                table: "PipelineRuns",
                column: "RunDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PipelineSteps_PipelineRunId",
                table: "PipelineSteps",
                column: "PipelineRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsStories");

            migrationBuilder.DropTable(
                name: "PipelineCandidates");

            migrationBuilder.DropTable(
                name: "PipelineSteps");

            migrationBuilder.DropTable(
                name: "PipelineRuns");
        }
    }
}
