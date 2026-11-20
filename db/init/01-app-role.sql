-- Kører kun, når databasen oprettes første gang (tom volume).
-- Rollens rettigheder gives i migrationen AddAppRole.
\getenv app_password POSTGRES_APP_PASSWORD
CREATE ROLE risk_app LOGIN PASSWORD :'app_password';