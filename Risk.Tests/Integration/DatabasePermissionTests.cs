using Npgsql;

namespace Risk.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class DatabasePermissionTests(ApiFixture api)
{
    [Theory]
    [InlineData("CREATE TABLE hacked (id int)")]
    [InlineData("DROP TABLE games")]
    [InlineData("ALTER TABLE games ADD COLUMN hacked int")]
    [InlineData("DELETE FROM \"__EFMigrationsHistory\"")]
    [InlineData("DELETE FROM map_territories")]
    public async Task The_api_role_can_only_change_game_data(string sql)
    {
        await using var connection = new NpgsqlConnection(api.AppConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }
}