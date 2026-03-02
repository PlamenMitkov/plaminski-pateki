using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcoTrails.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTrailEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModel",
                table: "Trails",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmbeddingUpdatedAt",
                table: "Trails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingVector",
                table: "Trails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EmbeddingModel", "EmbeddingUpdatedAt", "EmbeddingVector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "EmbeddingModel", "EmbeddingUpdatedAt", "EmbeddingVector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "EmbeddingModel", "EmbeddingUpdatedAt", "EmbeddingVector" },
                values: new object[] { null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbeddingModel",
                table: "Trails");

            migrationBuilder.DropColumn(
                name: "EmbeddingUpdatedAt",
                table: "Trails");

            migrationBuilder.DropColumn(
                name: "EmbeddingVector",
                table: "Trails");
        }
    }
}
