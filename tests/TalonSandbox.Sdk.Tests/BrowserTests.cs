using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace TalonSandbox.Sdk.Tests;

public class BrowserTests : WireMockFixture
{
    private const string SandboxJson = """
        {"id":"sb_abc","state":"running","image_id":"node:20","cpu_millis":0,
         "memory_bytes":0,"idle_timeout_seconds":0,"ttl_seconds":0,"created_at":0}
        """;

    [Fact]
    public async Task StartAsync_ReturnsCdpUrl()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/browser").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(201)
                  .WithBody("""{"cdp_ws_url":"ws://localhost:9222","profile_dir":"/data/chrome"}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var browser = await sb.Browser.StartAsync();
        browser.CdpUrl.Should().Be("ws://localhost:9222");
        browser.ProfileDir.Should().Be("/data/chrome");
    }

    [Fact]
    public async Task StopAsync_SendsDelete()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/browser").UsingDelete())
              .RespondWith(Response.Create().WithStatusCode(204));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        await sb.Browser.StopAsync();

        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.Method == "DELETE" && e.RequestMessage.Path.Contains("/browser"));
    }

    [Fact]
    public async Task GetAsync_ReturnsBrowserSession()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/browser").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"cdp_ws_url":"ws://localhost:9222","profile_dir":"/data/chrome"}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var session = await sb.Browser.GetAsync();
        session.CdpUrl.Should().StartWith("ws://");
    }
}
