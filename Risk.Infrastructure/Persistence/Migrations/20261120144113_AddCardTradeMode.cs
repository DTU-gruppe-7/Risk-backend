using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Risk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCardTradeMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasReceivedTerritoryBonusThisTurn",
                table: "games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CardTradeMode",
                table: "game_settings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                // Existing games keep the rules they were started with.
                defaultValue: "Progressive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasReceivedTerritoryBonusThisTurn",
                table: "games");

            migrationBuilder.DropColumn(
                name: "CardTradeMode",
                table: "game_settings");
        }
    }
}
