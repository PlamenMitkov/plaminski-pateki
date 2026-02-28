using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcoTrails.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSemanticTrailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DifficultyLevel",
                table: "Trails",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MaxAltitude",
                table: "Trails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredGear",
                table: "Trails",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "SuitableForKids",
                table: "Trails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WaterSources",
                table: "Trails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DifficultyLevel", "MaxAltitude", "RequiredGear", "SuitableForKids", "WaterSources" },
                values: new object[] { "moderate", 700, "туристически обувки, вода, ветровка", true, false });

            migrationBuilder.UpdateData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DifficultyLevel", "MaxAltitude", "RequiredGear", "SuitableForKids", "WaterSources" },
                values: new object[] { "moderate", 620, "удобни обувки, вода", true, true });

            migrationBuilder.UpdateData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DifficultyLevel", "MaxAltitude", "RequiredGear", "SuitableForKids", "WaterSources" },
                values: new object[] { "difficult", 1124, "високи туристически обувки, вода, щеки", false, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DifficultyLevel",
                table: "Trails");

            migrationBuilder.DropColumn(
                name: "MaxAltitude",
                table: "Trails");

            migrationBuilder.DropColumn(
                name: "RequiredGear",
                table: "Trails");

            migrationBuilder.DropColumn(
                name: "SuitableForKids",
                table: "Trails");

            migrationBuilder.DropColumn(
                name: "WaterSources",
                table: "Trails");
        }
    }
}
