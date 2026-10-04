using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Risk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameEntitiesAndRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bonus_cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Used = table.Column<bool>(type: "boolean", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TerritoryIdentifier = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bonus_cards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "continents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CaptureBonus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_continents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HostId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CurrentPlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentPhase = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_games_users_HostId",
                        column: x => x.HostId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameSetting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxPlayers = table.Column<int>(type: "integer", nullable: false),
                    Timer = table.Column<int>(type: "integer", nullable: false),
                    TurnTimeLimit = table.Column<int>(type: "integer", nullable: false),
                    HasFogOfWar = table.Column<bool>(type: "boolean", nullable: false),
                    HasMissions = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSetting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSetting_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Color = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReinforcementTroops = table.Column<int>(type: "integer", nullable: false),
                    IsEliminated = table.Column<bool>(type: "boolean", nullable: false),
                    TurnOrder = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_players_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_players_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mission_cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mission_cards_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mission_cards_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "territories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContinentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    TroopCount = table.Column<int>(type: "integer", nullable: false),
                    Identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    TerritoryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_territories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_territories_continents_ContinentId",
                        column: x => x.ContinentId,
                        principalTable: "continents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_territories_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_territories_games_GameId1",
                        column: x => x.GameId1,
                        principalTable: "games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_territories_players_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_territories_territories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "territories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "territory_borders",
                columns: table => new
                {
                    TerritoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    BorderingTerritoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_territory_borders", x => new { x.TerritoryId, x.BorderingTerritoryId });
                    table.ForeignKey(
                        name: "FK_territory_borders_territories_BorderingTerritoryId",
                        column: x => x.BorderingTerritoryId,
                        principalTable: "territories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_territory_borders_territories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "territories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bonus_cards_GameId",
                table: "bonus_cards",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_bonus_cards_PlayerId",
                table: "bonus_cards",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_continents_GameId",
                table: "continents",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_games_CurrentPlayerId",
                table: "games",
                column: "CurrentPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_games_HostId",
                table: "games",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSetting_GameId",
                table: "GameSetting",
                column: "GameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mission_cards_GameId",
                table: "mission_cards",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_mission_cards_PlayerId",
                table: "mission_cards",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_players_GameId_UserId",
                table: "players",
                columns: new[] { "GameId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_players_UserId",
                table: "players",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_territories_ContinentId",
                table: "territories",
                column: "ContinentId");

            migrationBuilder.CreateIndex(
                name: "IX_territories_GameId",
                table: "territories",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_territories_GameId1",
                table: "territories",
                column: "GameId1");

            migrationBuilder.CreateIndex(
                name: "IX_territories_OwnerId",
                table: "territories",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_territories_TerritoryId",
                table: "territories",
                column: "TerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_territory_borders_BorderingTerritoryId",
                table: "territory_borders",
                column: "BorderingTerritoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_bonus_cards_games_GameId",
                table: "bonus_cards",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_bonus_cards_players_PlayerId",
                table: "bonus_cards",
                column: "PlayerId",
                principalTable: "players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_continents_games_GameId",
                table: "continents",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_games_players_CurrentPlayerId",
                table: "games",
                column: "CurrentPlayerId",
                principalTable: "players",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_players_games_GameId",
                table: "players");

            migrationBuilder.DropTable(
                name: "bonus_cards");

            migrationBuilder.DropTable(
                name: "GameSetting");

            migrationBuilder.DropTable(
                name: "mission_cards");

            migrationBuilder.DropTable(
                name: "territory_borders");

            migrationBuilder.DropTable(
                name: "territories");

            migrationBuilder.DropTable(
                name: "continents");

            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropTable(
                name: "players");
        }
    }
}
