using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Risk.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(ApiFixture api)
{
    [Theory]
    [InlineData("violetmountainriverlamp")]    // kun små bogstaver
    [InlineData("violet mountain river lamp")] // mellemrum er tilladt
    public async Task Long_passwords_need_no_digits_capitals_or_symbols(string password)
    {
        var response = await TryRegisterAsync(password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Fourteen-Char1", "PasswordTooShort")]   // 14 tegn, opfylder de gamle regler
    [InlineData("password", "PasswordTooCommon")]
    public async Task Weak_passwords_are_rejected(string password, string errorCode)
    {
        var response = await TryRegisterAsync(password);

        await AssertRejectedAsync(response, errorCode);
    }

    [Fact]
    public async Task The_password_must_not_contain_the_email()
    {
        var name = $"alice{Guid.NewGuid():N}"[..16];

        var response = await TryRegisterAsync($"{name}-is-my-password", $"{name}@example.com");

        await AssertRejectedAsync(response, "PasswordContainsEmail");
    }

    private Task<HttpResponseMessage> TryRegisterAsync(string password, string? email = null)
    {
        var unique = Guid.NewGuid().ToString("N");
        return api.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { userName = unique[..20], email = email ?? $"{unique}@example.com", password });
    }

    private static async Task AssertRejectedAsync(HttpResponseMessage response, string errorCode)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(problem.GetProperty("errors").EnumerateObject(),
            error => error.Name.Equals(errorCode, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        var user = await api.RegisterAsync("lockout");

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(user.Http, user.Email, "Wrong-Password-123")).StatusCode);

        // Låst: selv det rigtige password afvises, med samme svar som et forkert.
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(user.Http, user.Email, ApiFixture.Password)).StatusCode);
    }

    [Fact]
    public async Task Too_many_login_attempts_from_one_address_are_rejected()
    {
        await using var limited = api.WithAuthPermitLimit(3);
        var client = limited.CreateClient();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, "nobody@example.com", "Wrong-Password-123")).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginAsync(client, "nobody@example.com", "Wrong-Password-123")).StatusCode);
    }

    [Fact]
    public async Task A_successful_login_resets_the_count_of_wrong_passwords()
    {
        var user = await api.RegisterAsync("reset");

        for (var i = 0; i < 4; i++)
            await LoginAsync(user.Http, user.Email, "Wrong-Password-123");
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Http, user.Email, ApiFixture.Password)).StatusCode);

        for (var i = 0; i < 4; i++)
            await LoginAsync(user.Http, user.Email, "Wrong-Password-123");
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Http, user.Email, ApiFixture.Password)).StatusCode);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });
}