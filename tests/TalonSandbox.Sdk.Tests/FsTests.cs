using System.Text;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace TalonSandbox.Sdk.Tests;

public class FsTests : WireMockFixture
{
    [Fact]
    public async Task ReadAsync_ReturnsBytes()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/fs/workspace/main.py").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody("print('hi')")
                  .WithHeader("Content-Type", "application/octet-stream"));

        var fs = new Fs("sb_abc", Client);
        var data = await fs.ReadAsync("/workspace/main.py");
        Encoding.UTF8.GetString(data).Should().Be("print('hi')");
    }

    [Fact]
    public async Task ReadTextAsync_ReturnsString()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/fs/workspace/hello.txt").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody("hello world")
                  .WithHeader("Content-Type", "application/octet-stream"));

        var fs = new Fs("sb_abc", Client);
        var text = await fs.ReadTextAsync("/workspace/hello.txt");
        text.Should().Be("hello world");
    }

    [Fact]
    public async Task WriteAsync_SendsPut()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/fs/workspace/x").UsingPut())
              .RespondWith(Response.Create().WithStatusCode(204));

        var fs = new Fs("sb_abc", Client);
        await fs.WriteAsync("/workspace/x", Encoding.UTF8.GetBytes("hello"));

        Server.LogEntries.Should().Contain(e => e.RequestMessage.Method == "PUT");
    }

    [Fact]
    public async Task WriteTextAsync_SendsPut()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/fs/workspace/readme.txt").UsingPut())
              .RespondWith(Response.Create().WithStatusCode(204));

        var fs = new Fs("sb_abc", Client);
        await fs.WriteTextAsync("/workspace/readme.txt", "hello sdk");

        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.Method == "PUT" && e.RequestMessage.Path.Contains("readme.txt"));
    }

    [Fact]
    public async Task ListAsync_ReturnsEntries()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/fs-list/workspace").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"entries":[{"name":"main.py","type":"file","size":10,"modified_at":0}]}""")
                  .WithHeader("Content-Type", "application/json"));

        var fs = new Fs("sb_abc", Client);
        var entries = await fs.ListAsync("/workspace");
        entries.Should().HaveCount(1);
        entries[0].Name.Should().Be("main.py");
        entries[0].Type.Should().Be("file");
    }

    [Fact]
    public async Task RemoveAsync_SendsDelete()
    {
        Server.Given(Request.Create().WithPath("/v1/sandboxes/sb_abc/fs/workspace/old").UsingDelete())
              .RespondWith(Response.Create().WithStatusCode(204));

        var fs = new Fs("sb_abc", Client);
        await fs.RemoveAsync("/workspace/old");

        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.Method == "DELETE" && e.RequestMessage.Path.Contains("/fs/"));
    }
}
