using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Risk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVictoryMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VictoryMode",
                table: "game_settings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "CompleteConquest");

            migrationBuilder.Sql(
                "UPDATE game_settings SET \"VictoryMode\" = 'Mission' WHERE \"HasMissions\" = TRUE;");

            migrationBuilder.DropColumn(
                name: "HasMissions",
                table: "game_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VictoryMode",
                table: "game_settings");

            migrationBuilder.AddColumn<bool>(
                name: "HasMissions",
                table: "game_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
