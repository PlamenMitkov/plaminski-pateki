using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcoTrails.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProposalStatus",
                table: "CommunityTrailPosts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "CommunityTrailPosts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "CommunityTrailPosts",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProposalStatus",
                table: "CommunityTrailPosts");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "CommunityTrailPosts");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "CommunityTrailPosts");
        }
    }
}
