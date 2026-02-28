using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcoTrails.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTrailSemanticFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "WaterSources",
                table: "Trails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "SuitableForKids",
                table: "Trails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "RequiredGear",
                table: "Trails",
                type: "nvarchar(1200)",
                maxLength: 1200,
                nullable: false,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "DifficultyLevel",
                table: "Trails",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Moderate",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.UpdateData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DifficultyLevel", "MaxAltitude", "RequiredGear" },
                values: new object[] { "Moderate", 780, "[\"туристически обувки\",\"вода\",\"дъждобран\"]" });

            migrationBuilder.UpdateData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DifficultyLevel", "MaxAltitude", "RequiredGear" },
                values: new object[] { "Moderate", 560, "[\"удобни обувки\",\"вода\",\"лека връхна дреха\"]" });

            migrationBuilder.UpdateData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DifficultyLevel", "MaxAltitude", "RequiredGear" },
                values: new object[] { "Difficult", 1060, "[\"високи туристически обувки\",\"щеки\",\"вода\",\"слойна екипировка\"]" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "WaterSources",
                table: "Trails",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "SuitableForKids",
                table: "Trails",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "RequiredGear",
                table: "Trails",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1200)",
                oldMaxLength: 1200,
                oldDefaultValue: "[]");

            migrationBuilder.AlterColumn<string>(
                name: "DifficultyLevel",
                table: "Trails",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "Moderate");

            migrationBuilder.UpdateData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DifficultyLevel", "MaxAltitude", "RequiredGear" },
                values: new object[] { "moderate", 700, "туристически обувки, вода, ветровка" });

            migrationBuilder.UpdateData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DifficultyLevel", "MaxAltitude", "RequiredGear" },
                values: new object[] { "moderate", 620, "удобни обувки, вода" });

            migrationBuilder.UpdateData(
                table: "Trails",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DifficultyLevel", "MaxAltitude", "RequiredGear" },
                values: new object[] { "difficult", 1124, "високи туристически обувки, вода, щеки" });
        }
    }
}
