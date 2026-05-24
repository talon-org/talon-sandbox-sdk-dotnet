using System.Text.Json.Serialization;

namespace TalonSandbox.Models;

/// <summary>Active headless browser session inside a sandbox.</summary>
public sealed class BrowserSession
{
    /// <summary>Chrome DevTools Protocol WebSocket URL.</summary>
    [JsonPropertyName("cdp_ws_url")]
    public string CdpUrl { get; set; } = string.Empty;

    [JsonPropertyName("profile_dir")]
    public string? ProfileDir { get; set; }
}
