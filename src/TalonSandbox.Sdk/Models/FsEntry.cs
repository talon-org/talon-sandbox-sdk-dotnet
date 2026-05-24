using System.Text.Json.Serialization;

namespace TalonSandbox.Models;

/// <summary>A single entry in a directory listing.</summary>
public sealed class FsEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;   // "file" | "directory"

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("modified_at")]
    public long ModifiedAt { get; set; }
}

internal sealed class FsListResponse
{
    [JsonPropertyName("entries")]
    public List<FsEntry> Entries { get; set; } = [];
}
