using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace TalonSandbox.Sdk.Tests;

/// <summary>
/// ListImagesAsync 测试（GET /v1/images）。
/// 这是顶层 client 方法，与特定 sandbox 无关。
/// </summary>
public class ImagesTests : WireMockFixture
{
    private const string ImagesResponseJson = """
        {
            "images": [
                {
                    "id": "img_abc",
                    "name": "node:20-bookworm",
                    "url": "ghcr.io/org/node:20",
                    "sha256": "abc123",
                    "os": "linux",
                    "arch": "amd64",
                    "source": "builtin",
                    "is_default": true,
                    "description": "Node.js 20 on Debian Bookworm",
                    "created_at": 1716556800
                },
                {
                    "id": "img_def",
                    "name": "python:3.12",
                    "url": "ghcr.io/org/python:3.12",
                    "sha256": "def456",
                    "os": "linux",
                    "arch": "amd64",
                    "source": "admin",
                    "is_default": false,
                    "created_at": 1716556900
                }
            ]
        }
        """;

    [Fact]
    public async Task ListImagesAsync_ReturnsImages()
    {
        Server.Given(Request.Create().WithPath("/v1/images").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody(ImagesResponseJson)
                  .WithHeader("Content-Type", "application/json"));

        var images = await Client.ListImagesAsync();

        images.Should().HaveCount(2);
        images[0].Id.Should().Be("img_abc");
        images[0].Name.Should().Be("node:20-bookworm");
        images[0].IsDefault.Should().BeTrue();
        images[0].Source.Should().Be("builtin");
        images[0].Os.Should().Be("linux");
        images[0].Arch.Should().Be("amd64");
        images[0].Description.Should().Be("Node.js 20 on Debian Bookworm");
        images[0].CreatedAt.Should().Be(1716556800);

        images[1].Id.Should().Be("img_def");
        images[1].IsDefault.Should().BeFalse();
        images[1].Source.Should().Be("admin");
    }

    [Fact]
    public async Task ListImagesAsync_EmptyList_ReturnsEmpty()
    {
        Server.Given(Request.Create().WithPath("/v1/images").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"images":[]}""")
                  .WithHeader("Content-Type", "application/json"));

        var images = await Client.ListImagesAsync();
        images.Should().BeEmpty();
    }

    [Fact]
    public async Task ListImagesAsync_SendsGetToCorrectPath()
    {
        Server.Given(Request.Create().WithPath("/v1/images").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200)
                  .WithBody("""{"images":[]}""")
                  .WithHeader("Content-Type", "application/json"));

        await Client.ListImagesAsync();

        Server.LogEntries.Should().Contain(e =>
            e.RequestMessage.Method == "GET" &&
            e.RequestMessage.Path == "/v1/images");
    }
}
