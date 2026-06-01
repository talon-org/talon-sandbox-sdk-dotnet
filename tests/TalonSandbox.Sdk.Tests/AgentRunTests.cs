using FluentAssertions;
using TalonSandbox.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace TalonSandbox.Sdk.Tests;

/// <summary>
/// AgentRunAsync 测试（POST /v1/sandboxes/{id}/agent/run）。
/// </summary>
public class AgentRunTests : WireMockFixture
{
    private const string SandboxJson = """
        {"id":"sb_abc","state":"running","image_id":"node:20","cpu_millis":0,
         "memory_bytes":0,"idle_timeout_seconds":0,"ttl_seconds":0,"created_at":0}
        """;

    // 完整响应体示例，字段与 dto.AgentRunResponse 对齐
    private const string AgentRunResponseJson = """
        {
            "run_id": "run_abc123",
            "status": "completed",
            "duration_ms": 12345,
            "steps": [
                {"step": 1, "action": "Page.navigate", "thought": "opening example.com"},
                {"step": 2, "action": "result", "thought": "done"}
            ],
            "result": "任务完成",
            "exit_code": 0,
            "stderr": ""
        }
        """;

    [Fact]
    public async Task AgentRunAsync_ReturnsResult()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/agent/run").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody(AgentRunResponseJson)
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var result = await sb.AgentRunAsync("打开 https://example.com");

        result.RunId.Should().Be("run_abc123");
        result.Status.Should().Be("completed");
        result.DurationMs.Should().Be(12345);
        result.ExitCode.Should().Be(0);
        result.Result.Should().Be("任务完成");
        result.Steps.Should().HaveCount(2);
        result.Steps[0].Step.Should().Be(1);
        result.Steps[0].Action.Should().Be("Page.navigate");
    }

    [Fact]
    public async Task AgentRunAsync_WithOptions_SendsMaxStepsAndModel()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/agent/run").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"run_id":"run_x","status":"completed","duration_ms":100,"steps":[],"exit_code":0}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var result = await sb.AgentRunAsync("目标", new AgentRunOptions
        {
            MaxSteps = 10,
            LlmModel = "anthropic:claude-sonnet-4-6",
        });

        result.Status.Should().Be("completed");

        // 验证请求体包含 max_steps 与 llm_model
        var entry = Server.LogEntries.First(e =>
            e.RequestMessage.Method == "POST" &&
            e.RequestMessage.Path.EndsWith("/agent/run"));
        var body = entry.RequestMessage.Body;
        body.Should().Contain("\"max_steps\"");
        body.Should().Contain("10");
        body.Should().Contain("\"llm_model\"");
        body.Should().Contain("claude-sonnet-4-6");
    }

    [Fact]
    public async Task AgentRunAsync_WithoutOptions_OmitsOptionalFields()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/agent/run").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"run_id":"run_y","status":"failed","duration_ms":500,"steps":[],"exit_code":1,"stderr":"error"}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var result = await sb.AgentRunAsync("目标");

        result.Status.Should().Be("failed");
        result.ExitCode.Should().Be(1);
        result.Stderr.Should().Be("error");

        // 未传 options 时 max_steps=0（WhenWritingDefault 忽略）、llm_model=null（WhenWritingNull 忽略）
        var entry = Server.LogEntries.First(e =>
            e.RequestMessage.Method == "POST" &&
            e.RequestMessage.Path.EndsWith("/agent/run"));
        var body = entry.RequestMessage.Body;
        body.Should().NotContain("\"max_steps\"");
        body.Should().NotContain("\"llm_model\"");
    }
}
