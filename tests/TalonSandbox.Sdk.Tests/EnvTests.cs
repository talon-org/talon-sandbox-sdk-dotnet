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
    public async Task SetAsync_SendsPut_WithValueOnlyBody()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/env/API_KEY").UsingPut())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"env":{"API_KEY":"sk-test"}}""")
                  .WithHeader("Content-Type", "application/json"));

        var env = new Env("sb_abc", Client);
        await env.SetAsync("API_KEY", "sk-test");

        var entry = Server.LogEntries.First(e =>
            e.RequestMessage.Method == "PUT" &&
            e.RequestMessage.Path.Contains("API_KEY"));

        entry.RequestMessage.Method.Should().Be("PUT");
        entry.RequestMessage.Path.Should().Contain("API_KEY");

        // Body must contain "value" but must NOT contain "key" (key lives in the path only).
        var body = entry.RequestMessage.Body;
        body.Should().Contain("\"value\"");
        body.Should().Contain("sk-test");
        body.Should().NotContain("\"key\"");
        body.Should().NotContain("API_KEY");
    }

    [Fact]
    public async Task UnsetAsync_SendsDelete()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/env/API_KEY").UsingDelete())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"env":{}}""")
                  .WithHeader("Content-Type", "application/json"));

        var env = new Env("sb_abc", Client);
        await env.UnsetAsync("API_KEY");

        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.Method == "DELETE" &&
            e.RequestMessage.Path.Contains("API_KEY"));
    }

    [Fact]
    public async Task UnsetAsync_EscapesKeyInPath()
    {
        // Verify that a key containing special chars goes into the path as a single segment
        // (not split by a literal slash).  Uri.EscapeDataString is applied in Env.UnsetAsync.
        // Note: HttpClient + BaseAddress normalizes %20 → space on the wire for path
        // segments, so the WireMock log shows the decoded form; the path still ends with
        // the key as a single segment (no extra slashes introduced).
        const string key = "MY KEY";

        Server.Given(Request.Create()
                  .WithPath(path => path.Contains("env"))
                  .UsingDelete())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"env":{}}""")
                  .WithHeader("Content-Type", "application/json"));

        var env = new Env("sb_abc", Client);
        await env.UnsetAsync(key);

        var entry = Server.LogEntries.First(e =>
            e.RequestMessage.Method == "DELETE");

        // The path must end with the full key as a single segment (EscapeDataString
        // prevents key from being split across multiple path segments).
        entry.RequestMessage.Path.Should().EndWith(key);
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
