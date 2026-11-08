using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Risk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVictoryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ObjectiveType",
                table: "mission_cards",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "ControlAllTerritories");

            migrationBuilder.AddColumn<string>(
                name: "TargetIdentifier",
                table: "mission_cards",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetPlayerId",
                table: "mission_cards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WinnerId",
                table: "games",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObjectiveType",
                table: "mission_cards");

            migrationBuilder.DropColumn(
                name: "TargetIdentifier",
                table: "mission_cards");

            migrationBuilder.DropColumn(
                name: "TargetPlayerId",
                table: "mission_cards");

            migrationBuilder.DropColumn(
                name: "WinnerId",
                table: "games");
        }
    }
}
