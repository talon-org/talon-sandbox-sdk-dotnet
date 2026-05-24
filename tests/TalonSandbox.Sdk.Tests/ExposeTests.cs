using FluentAssertions;
using TalonSandbox.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace TalonSandbox.Sdk.Tests;

public class ExposeTests : WireMockFixture
{
    private const string SandboxJson = """
        {"id":"sb_abc","state":"running","image_id":"node:20","cpu_millis":0,
         "memory_bytes":0,"idle_timeout_seconds":0,"ttl_seconds":0,"created_at":0}
        """;

    private void SetupGetSandbox() =>
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody(SandboxJson)
                  .WithHeader("Content-Type", "application/json"));

    [Fact]
    public async Task ExposeAsync_ReturnsUrl()
    {
        SetupGetSandbox();
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/expose").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"port":5173,"url":"https://sb-abc-5173.preview.example.com","signed":false}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var url = await sb.ExposeAsync(5173);
        url.Should().Be("https://sb-abc-5173.preview.example.com");
    }

    [Fact]
    public async Task ExposeAsync_WithSignOption_ReturnsSignedUrl()
    {
        SetupGetSandbox();
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/expose").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"port":5173,"url":"https://signed.example.com","signed":true,"expires_at":"2026-06-24T00:00:00Z"}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var url = await sb.ExposeAsync(5173, new() { Sign = true, Ttl = "1h" });
        url.Should().Contain("signed.example.com");
    }

    [Fact]
    public async Task ExposeAsync_404PageNotFound_ThrowsNotImplementedException()
    {
        // chi's default "404 page not found" body (no JSON, no "sandbox"
        // keyword) → endpoint genuinely missing on this server.
        SetupGetSandbox();
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/expose").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(404)
                  .WithBody("404 page not found"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var act = async () => await sb.ExposeAsync(5173);
        await act.Should().ThrowAsync<NotImplementedException>()
            .WithMessage("*Upgrade*");
    }

    [Fact]
    public async Task ExposeAsync_404SandboxNotFound_ThrowsNotFoundException()
    {
        // Server says the sandbox itself is gone — that's a real user error,
        // not a missing endpoint. NotFoundException must propagate so the
        // caller can distinguish "delete stale id" from "upgrade server".
        SetupGetSandbox();
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/expose").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(404)
                  .WithBody("""{"error":"sandbox not found"}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var act = async () => await sb.ExposeAsync(5173);
        await act.Should().ThrowAsync<Exceptions.NotFoundException>();
    }

    [Fact]
    public async Task UnexposeAsync_SendsDelete()
    {
        SetupGetSandbox();
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/expose/5173").UsingDelete())
              .RespondWith(Response.Create().WithStatusCode(204));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        await sb.UnexposeAsync(5173);

        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.Method == "DELETE" &&
            e.RequestMessage.Path.Contains("expose/5173"));
    }

    [Fact]
    public async Task ExposedAsync_ReturnsList()
    {
        SetupGetSandbox();
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/expose").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"ports":[{"port":5173,"url":"https://preview.example.com","signed":false}]}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var ports = await sb.ExposedAsync();
        ports.Should().HaveCount(1);
        ports[0].Port.Should().Be(5173);
        ports[0].Url.Should().Contain("preview.example.com");
    }

    [Fact]
    public async Task ExposeAsync_WithSubdomain_SendsSubdomain()
    {
        SetupGetSandbox();
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/expose").UsingPost())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"port":3000,"url":"https://my-app-3000.preview.example.com","signed":false}""")
                  .WithHeader("Content-Type", "application/json"));

        var sb = await Sandbox.GetAsync("sb_abc", Client);
        var url = await sb.ExposeAsync(3000, new ExposeOptions { Subdomain = "my-app" });
        url.Should().Contain("my-app");
    }
}
