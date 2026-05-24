using System.Text.Json.Serialization;

namespace TalonSandbox.Models;

/// <summary>A port exposed from the sandbox with its preview URL.</summary>
public sealed record ExposedPort
{
    [JsonPropertyName("port")]
    public int Port { get; init; }

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("signed")]
    public bool Signed { get; init; }

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }
}

internal sealed class ExposedPortListResponse
{
    [JsonPropertyName("ports")]
    public List<ExposedPort> Ports { get; set; } = [];
}

internal sealed class ExposeRequest
{
    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("sign")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Sign { get; set; }

    [JsonPropertyName("ttl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ttl { get; set; }

    [JsonPropertyName("subdomain")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Subdomain { get; set; }
}
