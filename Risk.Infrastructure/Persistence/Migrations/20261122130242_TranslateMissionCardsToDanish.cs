using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Risk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TranslateMissionCardsToDanish : Migration
    {
        // Continent id → Danish name. Must match the names in MissionDeck.
        private const string DanishNames = """
            (VALUES
                ('north-america', 'Nordamerika'),
                ('south-america', 'Sydamerika'),
                ('europe', 'Europa'),
                ('africa', 'Afrika'),
                ('asia', 'Asien'),
                ('australia', 'Australien')
            ) AS names(id, name)
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only cards that still carry the old generated English text are translated.
            migrationBuilder.Sql($"""
                UPDATE mission_cards AS m
                SET "Title" = 'Erobr ' || names.name,
                    "Description" = 'Kontrollér alle territorier i ' || names.name || '.'
                FROM {DanishNames}
                WHERE m."ObjectiveType" = 'ControlContinent'
                  AND m."TargetIdentifier" = names.id
                  AND m."Title" = 'Control ' || names.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE mission_cards AS m
                SET "Title" = 'Control ' || names.id,
                    "Description" = 'Control every territory in ' || names.id || '.'
                FROM {DanishNames}
                WHERE m."ObjectiveType" = 'ControlContinent'
                  AND m."TargetIdentifier" = names.id
                  AND m."Title" = 'Erobr ' || names.name;
                """);
        }
    }
}
