using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Risk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlizzards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Active",
                table: "territories",
                newName: "IsBlizzard");

            // The meaning is inverted (Active = true ⇔ IsBlizzard = false), so a plain
            // rename would turn every existing territory into a blizzard.
            migrationBuilder.Sql("""UPDATE territories SET "IsBlizzard" = NOT "IsBlizzard";""");

            migrationBuilder.AddColumn<int>(
                name: "BlizzardCount",
                table: "game_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlizzardCount",
                table: "game_settings");

            migrationBuilder.Sql("""UPDATE territories SET "IsBlizzard" = NOT "IsBlizzard";""");

            migrationBuilder.RenameColumn(
                name: "IsBlizzard",
                table: "territories",
                newName: "Active");
        }
    }
}
