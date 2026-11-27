using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Risk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "games",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Games already under way are past their first round, so missions can still win them.
            migrationBuilder.Sql("UPDATE games SET \"Round\" = 2 WHERE \"StartedAt\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Round",
                table: "games");
        }
    }
}
