using WireMock.Server;
using WireMock.Settings;

namespace TalonSandbox.Sdk.Tests;

public abstract class WireMockFixture : IDisposable
{
    protected WireMockServer Server { get; }
    protected SandboxClient Client { get; }

    protected WireMockFixture()
    {
        Server = WireMockServer.Start(new WireMockServerSettings { UseSSL = false });
        Client = new SandboxClient(Server.Url!, apiKey: "ask_test");
    }

    public void Dispose()
    {
        Client.Dispose();
        Server.Stop();
        Server.Dispose();
    }
}
