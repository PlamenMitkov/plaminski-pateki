using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EcoTrails.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialTrails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Trails",
                columns: new[] { "Id", "CreatedAt", "Description", "Difficulty", "DurationInHours", "ElevationGain", "Location", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 2, 4, 16, 36, 11, 0, DateTimeKind.Utc), "От Киреево тръгва екопътека, която носи името \"Ерантис Булгарикум\" и извежда до защитена местност \"Връшка чука\".", 3, 2.5, 200, "Киреево", "Екопътека \"Ерантис\" – Киреево" },
                    { 2, new DateTime(2026, 2, 4, 16, 36, 11, 0, DateTimeKind.Utc), "Трасето на пътеката минава през гориста местност и свързва Етъра и Соколския манастир.", 3, 2.0, 120, "Етър", "Екопътека \"Етър-Соколски манастир\"" },
                    { 3, new DateTime(2026, 2, 4, 16, 36, 11, 0, DateTimeKind.Utc), "Средно тежък кръгов маршрут с панорамни гледки към Белоградчишките скали и връх Ведерник.", 4, 4.0, 450, "Белоградчик", "Екопътека \"Збегове\" – Белоградчик" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}
