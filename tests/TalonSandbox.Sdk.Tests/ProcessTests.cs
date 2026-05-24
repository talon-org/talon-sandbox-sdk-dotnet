using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace TalonSandbox.Sdk.Tests;

public class ProcessTests : WireMockFixture
{
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
