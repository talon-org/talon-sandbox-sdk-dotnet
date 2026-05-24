using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace TalonSandbox.Sdk.Tests;

public class EnvTests : WireMockFixture
{
    [Fact]
    public async Task GetAsync_ReturnsValue()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/env/NODE_ENV").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"value":"development"}""")
                  .WithHeader("Content-Type", "application/json"));

        var env = new Env("sb_abc", Client);
        var val = await env.GetAsync("NODE_ENV");
        val.Should().Be("development");
    }

    [Fact]
    public async Task GetAsync_NullValue_ReturnsNull()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/env/MISSING").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"value":null}""")
                  .WithHeader("Content-Type", "application/json"));

        var env = new Env("sb_abc", Client);
        var val = await env.GetAsync("MISSING");
        val.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_SendsPut()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/env/API_KEY").UsingPut())
              .RespondWith(Response.Create().WithStatusCode(204));

        var env = new Env("sb_abc", Client);
        await env.SetAsync("API_KEY", "sk-test");

        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.Method == "PUT" && e.RequestMessage.Path.Contains("API_KEY"));
    }

    [Fact]
    public async Task AllAsync_ReturnsDictionary()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/env").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"env":{"NODE_ENV":"development","PORT":"3000"}}""")
                  .WithHeader("Content-Type", "application/json"));

        var env = new Env("sb_abc", Client);
        var all = await env.AllAsync();
        all.Should().ContainKey("NODE_ENV").WhoseValue.Should().Be("development");
        all.Should().ContainKey("PORT").WhoseValue.Should().Be("3000");
    }
}
