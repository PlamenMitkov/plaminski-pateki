using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcoTrails.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTrailEnrichmentSnapshotsAndCommunityPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityTrailPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TrailId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityTrailPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityTrailPosts_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityTrailPosts_Trails_TrailId",
                        column: x => x.TrailId,
                        principalTable: "Trails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TrailEnrichmentSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrailId = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourcePreviewFetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrailEnrichmentSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrailEnrichmentSnapshots_Trails_TrailId",
                        column: x => x.TrailId,
                        principalTable: "Trails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityTrailPostImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommunityTrailPostId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityTrailPostImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityTrailPostImages_CommunityTrailPosts_CommunityTrailPostId",
                        column: x => x.CommunityTrailPostId,
                        principalTable: "CommunityTrailPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityTrailPostImages_CommunityTrailPostId",
                table: "CommunityTrailPostImages",
                column: "CommunityTrailPostId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityTrailPosts_AppUserId_CreatedAtUtc",
                table: "CommunityTrailPosts",
                columns: new[] { "AppUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityTrailPosts_TrailId",
                table: "CommunityTrailPosts",
                column: "TrailId");

            migrationBuilder.CreateIndex(
                name: "IX_TrailEnrichmentSnapshots_TrailId_GeneratedAtUtc",
                table: "TrailEnrichmentSnapshots",
                columns: new[] { "TrailId", "GeneratedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityTrailPostImages");

            migrationBuilder.DropTable(
                name: "TrailEnrichmentSnapshots");

            migrationBuilder.DropTable(
                name: "CommunityTrailPosts");
        }
    }
}
