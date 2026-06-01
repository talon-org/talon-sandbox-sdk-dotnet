using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace TalonSandbox.Sdk.Tests;

/// <summary>
/// StartSandboxAsync / StopSandboxAsync 测试。
/// 两者均发 POST，返回 204 无 body（与 pause/resume 完全对称）。
/// </summary>
public class SandboxLifecycleTests : WireMockFixture
{
    private const string SandboxJson = """
        {"id":"sb_abc","state":"running","image_id":"node:20","cpu_millis":2000,
         "memory_bytes":4294967296,"idle_timeout_seconds":1800,"ttl_seconds":21600,
         "created_at":1716556800,"network_policy":"restricted-egress"}
        """;

    [Fact]
    public async Task StartSandboxAsync_Sends_POST_start()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/start").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(204));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        await sb.StartSandboxAsync();

        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.Method == "POST" && e.RequestMessage.Path.EndsWith("/start"));
    }

    [Fact]
    public async Task StopSandboxAsync_Sends_POST_stop()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/stop").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(204));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        await sb.StopSandboxAsync();

        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.Method == "POST" && e.RequestMessage.Path.EndsWith("/stop"));
    }
}
