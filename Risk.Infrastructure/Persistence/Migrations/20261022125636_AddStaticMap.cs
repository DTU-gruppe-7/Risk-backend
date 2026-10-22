using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Risk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaticMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_territories_continents_ContinentId",
                table: "territories");

            migrationBuilder.DropTable(
                name: "continents");

            migrationBuilder.DropTable(
                name: "territory_borders");

            migrationBuilder.DropIndex(
                name: "IX_territories_ContinentId",
                table: "territories");

            migrationBuilder.DropIndex(
                name: "IX_territories_GameId",
                table: "territories");

            migrationBuilder.DropColumn(
                name: "ContinentId",
                table: "territories");

            migrationBuilder.CreateTable(
                name: "map_continents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CaptureBonus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_map_continents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "map_territories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContinentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_map_territories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_map_territories_map_continents_ContinentId",
                        column: x => x.ContinentId,
                        principalTable: "map_continents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "map_borders",
                columns: table => new
                {
                    TerritoryId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NeighborId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_map_borders", x => new { x.TerritoryId, x.NeighborId });
                    table.ForeignKey(
                        name: "FK_map_borders_map_territories_NeighborId",
                        column: x => x.NeighborId,
                        principalTable: "map_territories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_map_borders_map_territories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "map_territories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "map_continents",
                columns: new[] { "Id", "CaptureBonus", "Name" },
                values: new object[,]
                {
                    { "africa", 3, "Africa" },
                    { "asia", 7, "Asia" },
                    { "australia", 2, "Australia" },
                    { "europe", 5, "Europe" },
                    { "north-america", 5, "North America" },
                    { "south-america", 2, "South America" }
                });

            migrationBuilder.InsertData(
                table: "map_territories",
                columns: new[] { "Id", "ContinentId" },
                values: new object[,]
                {
                    { "afghanistan", "asia" },
                    { "alaska", "north-america" },
                    { "alberta", "north-america" },
                    { "argentina", "south-america" },
                    { "brazil", "south-america" },
                    { "central-america", "north-america" },
                    { "china", "asia" },
                    { "congo", "africa" },
                    { "east-africa", "africa" },
                    { "eastern-australia", "australia" },
                    { "eastern-united-states", "north-america" },
                    { "egypt", "africa" },
                    { "great-britain", "europe" },
                    { "greenland", "north-america" },
                    { "iceland", "europe" },
                    { "india", "asia" },
                    { "indonesia", "australia" },
                    { "irkutsk", "asia" },
                    { "japan", "asia" },
                    { "kamchatka", "asia" },
                    { "madagascar", "africa" },
                    { "middle-east", "asia" },
                    { "mongolia", "asia" },
                    { "new-guinea", "australia" },
                    { "north-africa", "africa" },
                    { "northern-europe", "europe" },
                    { "northwest-territory", "north-america" },
                    { "ontario", "north-america" },
                    { "peru", "south-america" },
                    { "quebec", "north-america" },
                    { "scandinavia", "europe" },
                    { "siam", "asia" },
                    { "siberia", "asia" },
                    { "south-africa", "africa" },
                    { "southern-europe", "europe" },
                    { "ukraine", "europe" },
                    { "ural", "asia" },
                    { "venezuela", "south-america" },
                    { "western-australia", "australia" },
                    { "western-europe", "europe" },
                    { "western-united-states", "north-america" },
                    { "yakutsk", "asia" }
                });

            migrationBuilder.InsertData(
                table: "map_borders",
                columns: new[] { "NeighborId", "TerritoryId" },
                values: new object[,]
                {
                    { "china", "afghanistan" },
                    { "india", "afghanistan" },
                    { "middle-east", "afghanistan" },
                    { "ukraine", "afghanistan" },
                    { "ural", "afghanistan" },
                    { "alberta", "alaska" },
                    { "kamchatka", "alaska" },
                    { "northwest-territory", "alaska" },
                    { "alaska", "alberta" },
                    { "northwest-territory", "alberta" },
                    { "ontario", "alberta" },
                    { "western-united-states", "alberta" },
                    { "brazil", "argentina" },
                    { "peru", "argentina" },
                    { "argentina", "brazil" },
                    { "north-africa", "brazil" },
                    { "peru", "brazil" },
                    { "venezuela", "brazil" },
                    { "eastern-united-states", "central-america" },
                    { "venezuela", "central-america" },
                    { "western-united-states", "central-america" },
                    { "afghanistan", "china" },
                    { "india", "china" },
                    { "mongolia", "china" },
                    { "siam", "china" },
                    { "siberia", "china" },
                    { "ural", "china" },
                    { "east-africa", "congo" },
                    { "north-africa", "congo" },
                    { "south-africa", "congo" },
                    { "congo", "east-africa" },
                    { "egypt", "east-africa" },
                    { "madagascar", "east-africa" },
                    { "middle-east", "east-africa" },
                    { "north-africa", "east-africa" },
                    { "south-africa", "east-africa" },
                    { "new-guinea", "eastern-australia" },
                    { "western-australia", "eastern-australia" },
                    { "central-america", "eastern-united-states" },
                    { "ontario", "eastern-united-states" },
                    { "quebec", "eastern-united-states" },
                    { "western-united-states", "eastern-united-states" },
                    { "east-africa", "egypt" },
                    { "middle-east", "egypt" },
                    { "north-africa", "egypt" },
                    { "southern-europe", "egypt" },
                    { "iceland", "great-britain" },
                    { "northern-europe", "great-britain" },
                    { "scandinavia", "great-britain" },
                    { "western-europe", "great-britain" },
                    { "iceland", "greenland" },
                    { "northwest-territory", "greenland" },
                    { "ontario", "greenland" },
                    { "quebec", "greenland" },
                    { "great-britain", "iceland" },
                    { "greenland", "iceland" },
                    { "scandinavia", "iceland" },
                    { "afghanistan", "india" },
                    { "china", "india" },
                    { "middle-east", "india" },
                    { "siam", "india" },
                    { "new-guinea", "indonesia" },
                    { "siam", "indonesia" },
                    { "western-australia", "indonesia" },
                    { "kamchatka", "irkutsk" },
                    { "mongolia", "irkutsk" },
                    { "siberia", "irkutsk" },
                    { "yakutsk", "irkutsk" },
                    { "kamchatka", "japan" },
                    { "mongolia", "japan" },
                    { "alaska", "kamchatka" },
                    { "irkutsk", "kamchatka" },
                    { "japan", "kamchatka" },
                    { "mongolia", "kamchatka" },
                    { "yakutsk", "kamchatka" },
                    { "east-africa", "madagascar" },
                    { "south-africa", "madagascar" },
                    { "afghanistan", "middle-east" },
                    { "east-africa", "middle-east" },
                    { "egypt", "middle-east" },
                    { "india", "middle-east" },
                    { "southern-europe", "middle-east" },
                    { "ukraine", "middle-east" },
                    { "china", "mongolia" },
                    { "irkutsk", "mongolia" },
                    { "japan", "mongolia" },
                    { "kamchatka", "mongolia" },
                    { "siberia", "mongolia" },
                    { "eastern-australia", "new-guinea" },
                    { "indonesia", "new-guinea" },
                    { "western-australia", "new-guinea" },
                    { "brazil", "north-africa" },
                    { "congo", "north-africa" },
                    { "east-africa", "north-africa" },
                    { "egypt", "north-africa" },
                    { "southern-europe", "north-africa" },
                    { "western-europe", "north-africa" },
                    { "great-britain", "northern-europe" },
                    { "scandinavia", "northern-europe" },
                    { "southern-europe", "northern-europe" },
                    { "ukraine", "northern-europe" },
                    { "western-europe", "northern-europe" },
                    { "alaska", "northwest-territory" },
                    { "alberta", "northwest-territory" },
                    { "greenland", "northwest-territory" },
                    { "ontario", "northwest-territory" },
                    { "alberta", "ontario" },
                    { "eastern-united-states", "ontario" },
                    { "greenland", "ontario" },
                    { "northwest-territory", "ontario" },
                    { "quebec", "ontario" },
                    { "western-united-states", "ontario" },
                    { "argentina", "peru" },
                    { "brazil", "peru" },
                    { "venezuela", "peru" },
                    { "eastern-united-states", "quebec" },
                    { "greenland", "quebec" },
                    { "ontario", "quebec" },
                    { "great-britain", "scandinavia" },
                    { "iceland", "scandinavia" },
                    { "northern-europe", "scandinavia" },
                    { "ukraine", "scandinavia" },
                    { "china", "siam" },
                    { "india", "siam" },
                    { "indonesia", "siam" },
                    { "china", "siberia" },
                    { "irkutsk", "siberia" },
                    { "mongolia", "siberia" },
                    { "ural", "siberia" },
                    { "yakutsk", "siberia" },
                    { "congo", "south-africa" },
                    { "east-africa", "south-africa" },
                    { "madagascar", "south-africa" },
                    { "egypt", "southern-europe" },
                    { "middle-east", "southern-europe" },
                    { "north-africa", "southern-europe" },
                    { "northern-europe", "southern-europe" },
                    { "ukraine", "southern-europe" },
                    { "western-europe", "southern-europe" },
                    { "afghanistan", "ukraine" },
                    { "middle-east", "ukraine" },
                    { "northern-europe", "ukraine" },
                    { "scandinavia", "ukraine" },
                    { "southern-europe", "ukraine" },
                    { "ural", "ukraine" },
                    { "afghanistan", "ural" },
                    { "china", "ural" },
                    { "siberia", "ural" },
                    { "ukraine", "ural" },
                    { "brazil", "venezuela" },
                    { "central-america", "venezuela" },
                    { "peru", "venezuela" },
                    { "eastern-australia", "western-australia" },
                    { "indonesia", "western-australia" },
                    { "new-guinea", "western-australia" },
                    { "great-britain", "western-europe" },
                    { "north-africa", "western-europe" },
                    { "northern-europe", "western-europe" },
                    { "southern-europe", "western-europe" },
                    { "alberta", "western-united-states" },
                    { "central-america", "western-united-states" },
                    { "eastern-united-states", "western-united-states" },
                    { "ontario", "western-united-states" },
                    { "irkutsk", "yakutsk" },
                    { "kamchatka", "yakutsk" },
                    { "siberia", "yakutsk" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_territories_GameId_Identifier",
                table: "territories",
                columns: new[] { "GameId", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_territories_Identifier",
                table: "territories",
                column: "Identifier");

            migrationBuilder.CreateIndex(
                name: "IX_bonus_cards_TerritoryIdentifier",
                table: "bonus_cards",
                column: "TerritoryIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_map_borders_NeighborId",
                table: "map_borders",
                column: "NeighborId");

            migrationBuilder.CreateIndex(
                name: "IX_map_territories_ContinentId",
                table: "map_territories",
                column: "ContinentId");

            migrationBuilder.AddForeignKey(
                name: "FK_bonus_cards_map_territories_TerritoryIdentifier",
                table: "bonus_cards",
                column: "TerritoryIdentifier",
                principalTable: "map_territories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_territories_map_territories_Identifier",
                table: "territories",
                column: "Identifier",
                principalTable: "map_territories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bonus_cards_map_territories_TerritoryIdentifier",
                table: "bonus_cards");

            migrationBuilder.DropForeignKey(
                name: "FK_territories_map_territories_Identifier",
                table: "territories");

            migrationBuilder.DropTable(
                name: "map_borders");

            migrationBuilder.DropTable(
                name: "map_territories");

            migrationBuilder.DropTable(
                name: "map_continents");

            migrationBuilder.DropIndex(
                name: "IX_territories_GameId_Identifier",
                table: "territories");

            migrationBuilder.DropIndex(
                name: "IX_territories_Identifier",
                table: "territories");

            migrationBuilder.DropIndex(
                name: "IX_bonus_cards_TerritoryIdentifier",
                table: "bonus_cards");

            migrationBuilder.AddColumn<Guid>(
                name: "ContinentId",
                table: "territories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "continents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaptureBonus = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_continents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_continents_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_territories_ContinentId",
                table: "territories",
                column: "ContinentId");

            migrationBuilder.CreateIndex(
                name: "IX_territories_GameId",
                table: "territories",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_continents_GameId",
                table: "continents",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_territory_borders_BorderingTerritoryId",
                table: "territory_borders",
                column: "BorderingTerritoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_territories_continents_ContinentId",
                table: "territories",
                column: "ContinentId",
                principalTable: "continents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
