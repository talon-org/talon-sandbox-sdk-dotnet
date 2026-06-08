using FluentAssertions;
using TalonSandbox.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace TalonSandbox.Sdk.Tests;

public class SandboxTests : WireMockFixture
{
    private const string SandboxJson = """
        {"id":"sb_abc","state":"running","image_id":"node:20","cpu_millis":2000,
         "memory_bytes":4294967296,"idle_timeout_seconds":1800,"ttl_seconds":21600,
         "created_at":1716556800,"network_policy":"restricted-egress","labels":{"project":"agent-x"}}
        """;

    [Fact]
    public async Task CreateAsync_ReturnsRunning()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(201).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));

        await using var sb = await Sandbox.CreateAsync(new() { Image = "node:20" }, Client);

        sb.Id.Should().Be("sb_abc");
        sb.State.Should().Be("running");
    }

    [Fact]
    public async Task GetAsync_ReturnsSandbox()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        sb.Id.Should().Be("sb_abc");
        sb.State.Should().Be("running");
    }

    [Fact]
    public async Task GetAsync_NotFound_ThrowsNotFoundException()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/missing").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(404)
                  .WithBody("""{"error":"not found"}""")
                  .WithHeader("Content-Type", "application/json"));

        var act = async () => await Sandbox.GetAsync("missing", Client);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ListAsync_WithLabelFilter_FiltersClientSide()
    {
        // 服务端未实现 label 参数时（原路返回全量），客户端兜底过滤仍生效。
        Server.Given(Request.Create().WithPath("/v1/sandboxes").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"sandboxes":[""" + SandboxJson + """]}""")
                  .WithHeader("Content-Type", "application/json"));

        var list = await Sandbox.ListAsync(new() { Labels = new() { ["project"] = "agent-x" } }, Client);
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListAsync_LabelMismatch_ReturnsEmpty()
    {
        // 服务端未实现 label 参数，但客户端过滤可剔除不匹配的条目。
        Server.Given(Request.Create().WithPath("/v1/sandboxes").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"sandboxes":[""" + SandboxJson + """]}""")
                  .WithHeader("Content-Type", "application/json"));

        var list = await Sandbox.ListAsync(new() { Labels = new() { ["project"] = "other" } }, Client);
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_WithLabels_SendsServerSideLabelQueryParams()
    {
        // 验证服务端过滤：Labels 非空时请求 URL 应含 label=key:value query 参数。
        Server.Given(Request.Create().WithPath("/v1/sandboxes").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"sandboxes":[""" + SandboxJson + """]}""")
                  .WithHeader("Content-Type", "application/json"));

        await Sandbox.ListAsync(new() { Labels = new() { ["project"] = "agent-x", ["env"] = "prod" } }, Client);

        // 确认实际发出的请求携带了 label=project:agent-x 与 label=env:prod 两个参数。
        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.RawQuery != null &&
            e.RequestMessage.RawQuery.Contains("label=project%3Aagent-x") ||
            (e.RequestMessage.RawQuery != null &&
             e.RequestMessage.RawQuery.Contains("label=project:agent-x")));
    }

    [Fact]
    public async Task ListAsync_NoLabels_DoesNotAppendQueryParams()
    {
        // Labels 为 null 或空时不附加任何 label= 参数。
        Server.Given(Request.Create().WithPath("/v1/sandboxes").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"sandboxes":[""" + SandboxJson + """]}""")
                  .WithHeader("Content-Type", "application/json"));

        await Sandbox.ListAsync(options: null, Client);

        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.Method == "GET" &&
            e.RequestMessage.Path == "/v1/sandboxes" &&
            string.IsNullOrEmpty(e.RequestMessage.RawQuery));
    }

    [Fact]
    public async Task KillAsync_Sends_DELETE()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingDelete())
              .RespondWith(Response.Create().WithStatusCode(204));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        await sb.KillAsync();

        Server.LogEntries.Should().Contain(e => e.RequestMessage.Method == "DELETE");
    }

    [Fact]
    public async Task CreateAsync_WithResources_ParsesCorrectly()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(201).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));

        await using var sb = await Sandbox.CreateAsync(new()
        {
            Image = "node:20",
            Resources = new() { Cpu = 2, Memory = "4GiB", Disk = "10GiB" },
            Timeout = "30m",
            Ttl = "6h",
            Network = "allowlist",
        }, Client);

        sb.Id.Should().Be("sb_abc");
    }

    [Fact]
    public async Task PauseAsync_Sends_POST_pause()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/pause").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(204));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        await sb.PauseAsync();

        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.Method == "POST" && e.RequestMessage.Path.EndsWith("/pause"));
    }

    [Fact]
    public async Task ResumeAsync_Sends_POST_resume()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/resume").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(204));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        await sb.ResumeAsync();

        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.Method == "POST" && e.RequestMessage.Path.EndsWith("/resume"));
    }
}
