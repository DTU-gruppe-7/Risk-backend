using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Risk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConquestTurnState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasConqueredTerritoryThisTurn",
                table: "games",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasConqueredTerritoryThisTurn",
                table: "games");
        }
    }
}
