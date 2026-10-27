using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Risk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorDomainModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_territories_territories_TerritoryId",
                table: "territories");

            migrationBuilder.DropIndex(
                name: "IX_territories_TerritoryId",
                table: "territories");

            migrationBuilder.DropIndex(
                name: "IX_bonus_cards_PlayerId",
                table: "bonus_cards");

            migrationBuilder.DropColumn(
                name: "TerritoryId",
                table: "territories");

            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "users",
                newName: "Username");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "games",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_bonus_cards_PlayerId",
                table: "bonus_cards",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bonus_cards_PlayerId",
                table: "bonus_cards");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "games");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "users",
                newName: "DisplayName");

            migrationBuilder.AddColumn<Guid>(
                name: "TerritoryId",
                table: "territories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_territories_TerritoryId",
                table: "territories",
                column: "TerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_bonus_cards_PlayerId",
                table: "bonus_cards",
                column: "PlayerId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_territories_territories_TerritoryId",
                table: "territories",
                column: "TerritoryId",
                principalTable: "territories",
                principalColumn: "Id");
        }
    }
}
