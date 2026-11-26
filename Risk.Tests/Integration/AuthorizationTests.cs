using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Risk.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed partial class AuthorizationTests(ApiFixture api)
{
    [Fact]
    public async Task Every_endpoint_not_marked_AllowAnonymous_requires_login()
    {
        var client = api.CreateClient();
        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .ToList();
        Assert.NotEmpty(endpoints);

        foreach (var endpoint in endpoints)
        {
            var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.FirstOrDefault() ?? "GET";
            var path = "/" + RouteParameter().Replace(endpoint.RoutePattern.RawText!.TrimStart('/'), Guid.NewGuid().ToString());

            var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
                $"{method} {path} gav {(int)response.StatusCode} uden login.");
        }
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/readyz")]
    public async Task Health_checks_work_without_login(string path) =>
        Assert.Equal(HttpStatusCode.OK, (await api.CreateClient().GetAsync(path)).StatusCode);

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameter();
}