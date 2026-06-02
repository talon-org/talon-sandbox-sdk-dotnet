using System.Text.Json.Serialization;

namespace TalonSandbox.Models;

/// <summary>
/// 平台上可用的 baseimage 描述。与后端 dto.ImageDTO 字段一一对应。
/// 通过 <see cref="SandboxClient.ListImagesAsync"/> 获取列表。
/// </summary>
public sealed class ImageInfo
{
    /// <summary>Image 的唯一标识符，如 "img_abc123"。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>友好名称，如 "talon-alpine"。</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>OCI 镜像拉取地址。</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>镜像层 SHA256 校验摘要。</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>目标操作系统，通常 "linux"。</summary>
    [JsonPropertyName("os")]
    public string Os { get; set; } = string.Empty;

    /// <summary>CPU 架构，通常 "amd64"。</summary>
    [JsonPropertyName("arch")]
    public string Arch { get; set; } = string.Empty;

    /// <summary>来源："builtin"（内置）或 "admin"（管理员上传）。</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>是否为平台默认镜像（创建 sandbox 未指定 image_id 时的 fallback）。</summary>
    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }

    /// <summary>可选描述文字。</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>创建时间戳（Unix 秒）。</summary>
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }
}

/// <summary>GET /v1/images 的响应体。</summary>
internal sealed class ImageListResponse
{
    [JsonPropertyName("images")]
    public List<ImageInfo> Images { get; set; } = [];
}
