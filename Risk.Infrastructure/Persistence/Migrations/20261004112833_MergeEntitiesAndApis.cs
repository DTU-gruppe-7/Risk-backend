using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Risk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeEntitiesAndApis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bonus_cards_players_PlayerId",
                table: "bonus_cards");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSetting_games_GameId",
                table: "GameSetting");

            migrationBuilder.DropForeignKey(
                name: "FK_mission_cards_players_PlayerId",
                table: "mission_cards");

            migrationBuilder.DropForeignKey(
                name: "FK_territories_games_GameId1",
                table: "territories");

            migrationBuilder.DropIndex(
                name: "IX_territories_GameId1",
                table: "territories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GameSetting",
                table: "GameSetting");

            migrationBuilder.DropColumn(
                name: "GameId1",
                table: "territories");

            migrationBuilder.RenameTable(
                name: "GameSetting",
                newName: "game_settings");

            migrationBuilder.RenameIndex(
                name: "IX_GameSetting_GameId",
                table: "game_settings",
                newName: "IX_game_settings_GameId");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "mission_cards",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "mission_cards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "bonus_cards",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "TerritoryIdentifier",
                table: "bonus_cards",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_game_settings",
                table: "game_settings",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_bonus_cards_players_PlayerId",
                table: "bonus_cards",
                column: "PlayerId",
                principalTable: "players",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_game_settings_games_GameId",
                table: "game_settings",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_mission_cards_players_PlayerId",
                table: "mission_cards",
                column: "PlayerId",
                principalTable: "players",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bonus_cards_players_PlayerId",
                table: "bonus_cards");

            migrationBuilder.DropForeignKey(
                name: "FK_game_settings_games_GameId",
                table: "game_settings");

            migrationBuilder.DropForeignKey(
                name: "FK_mission_cards_players_PlayerId",
                table: "mission_cards");

            migrationBuilder.DropPrimaryKey(
                name: "PK_game_settings",
                table: "game_settings");

            migrationBuilder.RenameTable(
                name: "game_settings",
                newName: "GameSetting");

            migrationBuilder.RenameIndex(
                name: "IX_game_settings_GameId",
                table: "GameSetting",
                newName: "IX_GameSetting_GameId");

            migrationBuilder.AddColumn<Guid>(
                name: "GameId1",
                table: "territories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "mission_cards",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "mission_cards",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "bonus_cards",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "TerritoryIdentifier",
                table: "bonus_cards",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameSetting",
                table: "GameSetting",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_territories_GameId1",
                table: "territories",
                column: "GameId1");

            migrationBuilder.AddForeignKey(
                name: "FK_bonus_cards_players_PlayerId",
                table: "bonus_cards",
                column: "PlayerId",
                principalTable: "players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GameSetting_games_GameId",
                table: "GameSetting",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_mission_cards_players_PlayerId",
                table: "mission_cards",
                column: "PlayerId",
                principalTable: "players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_territories_games_GameId1",
                table: "territories",
                column: "GameId1",
                principalTable: "games",
                principalColumn: "Id");
        }
    }
}
