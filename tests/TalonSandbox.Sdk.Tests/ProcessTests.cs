using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace TalonSandbox.Sdk.Tests;

public class ProcessTests : WireMockFixture
{
    // ── env / expose_ports 序列化测试 ────────────────────────────────────────

    [Fact]
    public async Task SpawnAsync_WithEnvAndExposePorts_SendsFieldsInBody()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(201)
                  .WithBody("""{"id":"proc_env","pid":10,"command":["/bin/sh","-c","node"],"status":"running"}""")
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"processes":[{"id":"proc_env","pid":10,"command":[],"status":"exited","exit_code":0}]}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var proc = await sb.SpawnAsync(
            "node server.js",
            cwd: "/app",
            env: ["PORT=3000", "NODE_ENV=production"],
            exposePorts: [3000, 8080]);

        proc.Id.Should().Be("proc_env");

        // 验证请求体包含 env 与 expose_ports 字段
        var entry = Server.LogEntries.First(e =>
            e.RequestMessage.Method == "POST" &&
            e.RequestMessage.Path.EndsWith("/processes"));
        var body = entry.RequestMessage.Body;
        body.Should().Contain("\"env\"");
        body.Should().Contain("PORT=3000");
        body.Should().Contain("NODE_ENV=production");
        body.Should().Contain("\"expose_ports\"");
        body.Should().Contain("3000");
        body.Should().Contain("8080");
    }

    [Fact]
    public async Task SpawnAsync_NoEnvNoExposePorts_OmitsFieldsFromBody()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(201)
                  .WithBody("""{"id":"proc_min","pid":11,"command":["/bin/sh","-c","sleep 1"],"status":"running"}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        // env 与 exposePorts 均使用默认 null
        var proc = await sb.SpawnAsync("sleep 1");

        proc.Id.Should().Be("proc_min");

        var entry = Server.LogEntries.First(e =>
            e.RequestMessage.Method == "POST" &&
            e.RequestMessage.Path.EndsWith("/processes"));
        var body = entry.RequestMessage.Body;
        // null 字段应被序列化忽略，不出现在 body 中
        body.Should().NotContain("\"env\"");
        body.Should().NotContain("\"expose_ports\"");
    }

    [Fact]
    public async Task RunAsync_WithEnv_SendsEnvInBody()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/exec").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"stdout":"production","stderr":"","exit_code":0,"duration_ms":5}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var result = await sb.RunAsync(
            "echo $NODE_ENV",
            env: ["NODE_ENV=production"]);

        result.ExitCode.Should().Be(0);

        // 验证请求体包含 env 字段
        var entry = Server.LogEntries.First(e =>
            e.RequestMessage.Method == "POST" &&
            e.RequestMessage.Path.EndsWith("/exec"));
        var body = entry.RequestMessage.Body;
        body.Should().Contain("\"env\"");
        body.Should().Contain("NODE_ENV=production");
    }

    [Fact]
    public async Task RunAsync_NoEnv_OmitsEnvFromBody()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/exec").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"stdout":"ok","stderr":"","exit_code":0,"duration_ms":3}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        await sb.RunAsync("echo ok");

        var entry = Server.LogEntries.First(e =>
            e.RequestMessage.Method == "POST" &&
            e.RequestMessage.Path.EndsWith("/exec"));
        var body = entry.RequestMessage.Body;
        // env 为 null 时不发送该字段
        body.Should().NotContain("\"env\"");
    }


    private const string SandboxJson = """
        {"id":"sb_abc","state":"running","image_id":"node:20","cpu_millis":2000,
         "memory_bytes":4294967296,"idle_timeout_seconds":0,"ttl_seconds":0,"created_at":0}
        """;

    [Fact]
    public async Task RunAsync_ReturnsProcessResult()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/exec").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"stdout":"hello","stderr":"","exit_code":0,"duration_ms":12.5}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var result = await sb.RunAsync("echo hello");

        result.Stdout.Should().Be("hello");
        result.Stderr.Should().Be(string.Empty);
        result.ExitCode.Should().Be(0);
        result.DurationMs.Should().Be(12.5);
    }

    [Fact]
    public async Task RunAsync_NonZeroExitCode_IsReflected()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/exec").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"stdout":"","stderr":"command not found","exit_code":127,"duration_ms":5}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var result = await sb.RunAsync("nosuchcmd");

        result.ExitCode.Should().Be(127);
        result.Stderr.Should().Contain("not found");
    }

    [Fact]
    public async Task SpawnAsync_ReturnsSandboxProcess()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(201)
                  .WithBody("""{"id":"proc_xyz","pid":42,"command":["/bin/sh","-c","npm run dev"],"status":"running"}""")
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes/proc_xyz/logs").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody("line1\nline2")
                  .WithHeader("Content-Type", "text/plain"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"processes":[{"id":"proc_xyz","pid":42,"command":[],"status":"exited","exit_code":0}]}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var chunks = new List<string>();
        var proc = await sb.SpawnAsync("npm run dev");
        proc.Id.Should().Be("proc_xyz");
        proc.StdoutReceived += (_, e) => chunks.Add(e.Line);

        await proc.WaitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        string.Concat(chunks).Should().Contain("line1").And.Contain("line2");
    }

    // ── C4 regression: exit code + log offset correctness ───────────────

    [Fact]
    public async Task SpawnAsync_WaitAsync_ReturnsExitCodeFromServer()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(201)
                  .WithBody("""{"id":"proc_xyz","pid":1,"command":["false"],"status":"running"}""")
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes/proc_xyz/logs").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(""));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"processes":[{"id":"proc_xyz","pid":1,"command":[],"status":"exited","exit_code":42}]}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var proc = await sb.SpawnAsync("false");
        var code = await proc.WaitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        code.Should().Be(42);
    }

    [Fact]
    public async Task SpawnAsync_KillAsync_FiresExitedEvent()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(201)
                  .WithBody("""{"id":"proc_xyz","pid":1,"command":["sleep"],"status":"running"}""")
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes/proc_xyz/logs").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(""));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"processes":[{"id":"proc_xyz","pid":1,"command":[],"status":"running"}]}""")
                  .WithHeader("Content-Type", "application/json"));
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/processes/proc_xyz").UsingDelete())
              .RespondWith(Response.Create().WithStatusCode(204));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var proc = await sb.SpawnAsync("sleep 999");
        var exitFired = new TaskCompletionSource<int>();
        proc.Exited += (_, code) => exitFired.TrySetResult(code);

        await proc.KillAsync();
        var code = await exitFired.Task.WaitAsync(TimeSpan.FromSeconds(2));
        // -1 is our "killed by client" sentinel.
        code.Should().Be(-1);
    }
}
