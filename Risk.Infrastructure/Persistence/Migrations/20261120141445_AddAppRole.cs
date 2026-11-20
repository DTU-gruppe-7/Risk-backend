using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Risk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // API'et kører som risk_app: læse og skrive data, aldrig ændre skemaet.
            // Rollens password sættes uden for migrationen: db/init lokalt, manuelt på serverne.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'risk_app') THEN
                        CREATE ROLE risk_app LOGIN;
                    END IF;
                END
                $$;

                GRANT USAGE ON SCHEMA public TO risk_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO risk_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO risk_app;

                -- Kortet er statisk og kommer fra migrationer, og migrationshistorikken er migratorens.
                REVOKE INSERT, UPDATE, DELETE ON map_borders, map_continents, map_territories FROM risk_app;
                REVOKE ALL ON "__EFMigrationsHistory" FROM risk_app;

                -- Tabeller fra senere migrationer får automatisk de samme rettigheder.
                ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO risk_app;
                ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO risk_app;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollen slettes ikke. Den kan være oprettet af en administrator og bruges af API'et.
            migrationBuilder.Sql("""
                ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON TABLES FROM risk_app;
                ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON SEQUENCES FROM risk_app;
                REVOKE ALL ON ALL TABLES IN SCHEMA public FROM risk_app;
                REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM risk_app;
                REVOKE USAGE ON SCHEMA public FROM risk_app;
                """);
        }
    }
}
